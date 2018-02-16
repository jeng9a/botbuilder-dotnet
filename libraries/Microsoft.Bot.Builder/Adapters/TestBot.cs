﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;

namespace Microsoft.Bot.Builder.Adapters
{
    public class TestBot : BotBase
    {
        private int _nextId = 0;
        private readonly List<IActivity> botReplies = new List<IActivity>();
        private Func<IBotContext, Task> callback;

        public TestBot(ConversationReference reference = null)
        {
            if (reference != null)
            {
                this.ConversationReference = reference;
            }
            else
            {
                this.ConversationReference = new ConversationReference
                {
                    ChannelId = "test",
                    ServiceUrl = "https://test.com"
                };

                this.ConversationReference.User = new ChannelAccount("user1", "User1");
                this.ConversationReference.Bot = new ChannelAccount("bot", "Bot");
                this.ConversationReference.Conversation = new ConversationAccount(false, "convo1", "Conversation1");
            }
        }


        public new TestBot Use(Middleware.IMiddleware middleware)
        {
            base._middlewareSet.Use(middleware);
            return this;
        }

        public Task ProcessActivity(IActivity activity, Func<IBotContext, Task> callback)
        {
            lock (this.ConversationReference)
            {
                // ready for next reply
                if (activity.Type == null)
                    activity.Type = ActivityTypes.Message;
                activity.ChannelId = this.ConversationReference.ChannelId;
                activity.From = this.ConversationReference.User;
                activity.Recipient = this.ConversationReference.Bot;
                activity.Conversation = this.ConversationReference.Conversation;
                activity.ServiceUrl = this.ConversationReference.ServiceUrl;

                var id = activity.Id = (this._nextId++).ToString();
            }

            return base.ProcessActivityInternal(activity, callback);
        }

        public ConversationReference ConversationReference { get; set; }


        protected async override Task SendActivityImplementation(IBotContext context, IActivity activity)
        {
            if (activity.Type == "delay")
            {
                // The BotFrameworkAdapter and Console adapter implement this
                // hack directly in the POST method. Replicating that here
                // to keep the behavior as close as possible to facillitate
                // more realistic tests.                     
                int delayMs = (int)((Activity)activity).Value;
                await Task.Delay(delayMs);
            }
            else
            {
                lock (this.botReplies)
                {
                    this.botReplies.Add(activity);
                }
            }
        }

        protected override Task<ResourceResponse> UpdateActivityImplementation(IBotContext context, IActivity activity)
        {
            lock (this.botReplies)
            {
                for (int i = 0; i < this.botReplies.Count; i++)
                {
                    if (this.botReplies[i].Id == activity.Id)
                    {
                        this.botReplies[i] = activity;
                        return Task.FromResult(new ResourceResponse(activity.Id));
                    }
                }
            }
            return Task.FromResult(new ResourceResponse());
        }

        protected override Task DeleteActivityImplementation(IBotContext context, string conversationId, string activityId)
        {
            lock (this.botReplies)
            {
                for (int i = 0; i < this.botReplies.Count; i++)
                {
                    if (this.botReplies[i].Id == activityId)
                    {
                        this.botReplies.RemoveAt(i);
                        break;
                    }
                }
            }
            return Task.CompletedTask;
        }

        protected override Task CreateConversationImplementation()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called by TestFlow to check next reply
        /// </summary>
        /// <returns></returns>
        public IActivity GetNextReply()
        {
            lock (this.botReplies)
            {
                if (this.botReplies.Count > 0)
                {
                    var result = this.botReplies[0];
                    this.botReplies.RemoveAt(0);
                    return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Called by TestFlow to get appropriate activity for conversationReference of testbot
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public IActivity MakeActivity(string text = null)
        {
            Activity activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = ConversationReference.User,
                Recipient = ConversationReference.Bot,
                Conversation = ConversationReference.Conversation,
                ServiceUrl = ConversationReference.ServiceUrl,
                Id = (_nextId++).ToString(),
                Text = text
            };

            return activity;
        }


        /// <summary>
        /// Called by TestFlow to send text to the bot
        /// </summary>
        /// <param name="userSays"></param>
        /// <returns></returns>
        public Task SendTextToBot(string userSays, Func<IBotContext, Task> callback)
        {
            return this.ProcessActivity(this.MakeActivity(userSays), callback);
        }
    }


    public class TestFlow
    {
        readonly TestBot bot;
        readonly Task testTask;
        Func<IBotContext, Task> callback;

        public TestFlow(TestBot bot, Func<IBotContext, Task> callback = null)
        {
            this.bot = bot;
            this.callback = callback;
            this.testTask = testTask ?? Task.CompletedTask;
        }

        public TestFlow(Task testTask, TestFlow flow)
        {
            this.testTask = testTask ?? Task.CompletedTask;
            this.callback = flow.callback;
            this.bot = flow.bot;
        }

        /// <summary>
        /// Start the execution of the test flow
        /// </summary>
        /// <returns></returns>
        public Task StartTest()
        {
            return this.testTask;
        }

        /// <summary>
        /// Send a message from the user to the bot
        /// </summary>
        /// <param name="userSays"></param>
        /// <returns></returns>
        public TestFlow Send(string userSays)
        {
            if (userSays == null)
                throw new ArgumentNullException("You have to pass a userSays parameter");

            return new TestFlow(this.testTask.ContinueWith((task) =>
            {
                if (task.IsFaulted)
                    throw new Exception("failed");
                return this.bot.SendTextToBot(userSays, this.callback);
            }), this);
        }

        /// <summary>
        /// Send an activity from the user to the bot
        /// </summary>
        /// <param name="userActivity"></param>
        /// <returns></returns>
        public TestFlow Send(IActivity userActivity)
        {
            if (userActivity == null)
                throw new ArgumentNullException("You have to pass an Activity");

            return new TestFlow(this.testTask.ContinueWith((task) =>
            {
                if (task.IsFaulted)
                    throw new Exception("failed");
                return this.bot.ProcessActivity(userActivity, this.callback);
            }), this);
        }

        /// <summary>
        /// Delay for time period 
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        public TestFlow Delay(UInt32 ms)
        {
            return new TestFlow(this.testTask.ContinueWith((task) =>
            {
                if (task.IsFaulted)
                    throw new Exception("failed");
                return Task.Delay((int)ms);
            }), this);
        }

        /// <summary>
        /// Assert that reply is expected text
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow AssertReply(string expected, string description = null, UInt32 timeout = 3000)
        {
            return this.AssertReply(this.bot.MakeActivity(expected), description, timeout);
        }

        /// <summary>
        /// Assert that the reply is expected activity 
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow AssertReply(IActivity expected, string description = null, UInt32 timeout = 3000)
        {
            return this.AssertReply((reply) =>
            {
                if (expected.Type != reply.Type)
                    throw new Exception($"{description}: Type should match");
                if (expected.AsMessageActivity().Text != reply.AsMessageActivity().Text)
                    throw new Exception($"{description}: Text should match");
                // TODO, expand this to do all properties set on expected
            }, description, timeout);
        }

        /// <summary>
        /// Assert that the reply matches a custom validation routine
        /// </summary>
        /// <param name="validateActivity"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow AssertReply(Action<IActivity> validateActivity, string description, UInt32 timeout = 3000)
        {
            return new TestFlow(this.testTask.ContinueWith((task) =>
            {
                if (task.IsFaulted)
                    throw new Exception("failed");
                var start = DateTime.UtcNow;
                while (true)
                {
                    var current = DateTime.UtcNow;

                    if ((current - start).TotalMilliseconds > timeout)
                    {
                        throw new TimeoutException($"{timeout}ms Timed out waiting for:${description}");
                    }

                    IActivity replyActivity = this.bot.GetNextReply();
                    if (replyActivity != null)
                    {
                        // if we have a reply
                        validateActivity(replyActivity);
                        return;
                    }
                }
            }), this);
        }


        /// <summary>
        /// Say() -> shortcut for .Send(user).AssertReply(Expected)
        /// </summary>
        /// <param name="userSays"></param>
        /// <param name="expected"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow Test(string userSays, string expected, string description = null, UInt32 timeout = 3000)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));

            return this.Send(userSays)
                .AssertReply(expected, description, timeout);
        }

        /// <summary>
        /// Test() -> shortcut for .Send(user).AssertReply(Expected)
        /// </summary>
        /// <param name="userSays"></param>
        /// <param name="expected"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow Test(string userSays, Activity expected, string description = null, UInt32 timeout = 3000)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));

            return this.Send(userSays)
                .AssertReply(expected, description, timeout);
        }

        /// <summary>
        /// Say() -> shortcut for .Send(user).AssertReply(Expected)
        /// </summary>
        /// <param name="userSays"></param>
        /// <param name="expected"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow Test(string userSays, Action<IActivity> expected, string description = null, UInt32 timeout = 3000)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));

            return this.Send(userSays)
                .AssertReply(expected, description, timeout);
        }

        /// <summary>
        /// Assert that reply is one of the candidate responses
        /// </summary>
        /// <param name="candidates"></param>
        /// <param name="description"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public TestFlow AssertReplyOneOf(string[] candidates, string description = null, UInt32 timeout = 3000)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            return this.AssertReply((reply) =>
            {
                foreach (var candidate in candidates)
                {
                    if (reply.AsMessageActivity().Text == candidate)
                        return;
                }
                throw new Exception(description ?? $"Not one of candidates: {String.Join("\n", candidates)}");
            }, description, timeout);
        }
    }
}