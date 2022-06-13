using Penguin.Cms.Email.Abstractions;
using Penguin.Cms.Entities;
using Penguin.Cms.Repositories;
using Penguin.Configuration.Abstractions.Extensions;
using Penguin.Configuration.Abstractions.Interfaces;
using Penguin.Messaging.Core;
using Penguin.Messaging.Persistence.Messages;
using Penguin.Persistence.Abstractions;
using Penguin.Persistence.Abstractions.Interfaces;
using Penguin.Persistence.Database;
using Penguin.Persistence.Database.Objects;
using Penguin.Persistence.Repositories;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Penguin.Cms.Email.Repositories
{
    /// <summary>
    /// An IRepository implementation used to access/persist a queue of email messages
    /// </summary>
    public class EmailRepository : EntityRepository<EmailMessage>
    {
        /// <summary>
        /// The optional Database Connection info used to bypass the persistence context for updating email state
        /// </summary>
        public PersistenceConnectionInfo PersistenceConnectionInfo { get; protected set; }

        /// <summary>
        /// The current Configuration Service instance used by this repository
        /// </summary>
        protected IProvideConfigurations ConfigurationService { get; set; }

        private DatabaseInstance DatabaseInstance { get; set; }

        /// <summary>
        /// Creates a new instance of this Email repository
        /// </summary>
        /// <param name="connectionInfo">Optional database connection info representing the database the emails are stored in. If provided, allows the repository to use ADO to update sent state as failsafe</param>
        /// <param name="configurationService">A Configuration Service instance used to extract values for the email system</param>
        /// <param name="dbContext">The Persistence context used to store EmailMessages</param>
        /// <param name="messageBus">An optional message bus used to send email message events</param>
        public EmailRepository(PersistenceConnectionInfo connectionInfo, IProvideConfigurations configurationService, IPersistenceContext<EmailMessage> dbContext, MessageBus messageBus = null) : base(dbContext, messageBus)
        {
            this.ConfigurationService = configurationService;
            PersistenceConnectionInfo = connectionInfo;

            if (connectionInfo != null)
            {
                DatabaseInstance = new DatabaseInstance(connectionInfo.ConnectionString);
            }
        }

        /// <summary>
        /// A message handler for handling changes to email messages. Used to set the state to "debug" if the website is in debug mode, per the configuration service
        /// </summary>
        /// <param name="updateMessage">The message containing the email being updated.</param>
        public void AcceptMessage(Updating<EmailMessage> updateMessage)
        {
            Contract.Requires(updateMessage != null);

            if (this.ConfigurationService.IsDebug())
            {
                updateMessage.Target.State = EmailMessageState.Debug;
            }
        }

        /// <summary>
        /// Retrieves all email messages with a given state
        /// </summary>
        /// <param name="target">The state to retrieve</param>
        /// <returns>All email messages with a given state</returns>
        public IEnumerable<EmailMessage> GetByState(EmailMessageState target) => this.Where(e => e.State == target);

        /// <summary>
        /// Gets a list of emails for which the send-date has passed, but no attempt has been made to send
        /// </summary>
        /// <param name="limit">The max number of emails to retrieve, or 0 if all</param>
        /// <returns>Emails for which the send-date has passed, but no attempt has been made to send</returns>
        public IEnumerable<EmailMessage> GetScheduledEmails(int limit = 0)
        {
            IQueryable<EmailMessage> toReturn = this.Where(e => e.State == EmailMessageState.Unsent && e.SendDate < DateTime.Now);

            if (limit != 0)
            {
                toReturn = toReturn.Take(limit);
            }

            return toReturn;
        }

        /// <summary>
        /// Updates the state of the email message and DOES NOT wait for the context to commit if there is an associated database connection string
        /// </summary>
        /// <param name="messageId">The email message to update</param>
        /// <param name="toSet">the state to set it in</param>
        public void UpdateState(int messageId, EmailMessageState toSet)
        {
            if (DatabaseInstance is null)
            {
                this.Context.Where(e => e._Id == messageId).Single().State = toSet;
            }
            else
            {
                DatabaseInstance.Execute($"update emailmessages set state = {(int)toSet} where {nameof(Entity._Id)} = {messageId}");
            }
        }
    }
}