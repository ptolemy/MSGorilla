﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MSGorilla.Library.Azure;
using MSGorilla.Library.Models;
using MSGorilla.Library.Models.SqlModels;
using MSGorilla.Library.Models.AzureModels;
using MSGorilla.Library.Exceptions;
using MSGorilla.Library.Models.AzureModels.Entity;


namespace MSGorilla.Library
{
    public class PostManager
    {
        private CloudQueue _queue;
        private CloudTable _homelineTweet;
        private CloudTable _userlineTweet;
        private CloudTable _reply;
        private CloudTable _replyNotification;

        private AccountManager _accManager;

        public PostManager(){
            _queue = AzureFactory.GetQueue();
            _homelineTweet = AzureFactory.GetTable(AzureFactory.TweetTable.HomelineTweet);
            _userlineTweet = AzureFactory.GetTable(AzureFactory.TweetTable.UserlineTweet);
            _reply = AzureFactory.GetTable(AzureFactory.TweetTable.Reply);
            _replyNotification = AzureFactory.GetTable(AzureFactory.TweetTable.ReplyNotification);

            _accManager = new AccountManager();
        }

        public void PostTweet(string userid, string tweetType, string message, DateTime timestamp, string url = "")
        {
            if (message.Length > 256)
            {
                throw new MessageTooLongException();
            }
            if (_accManager.FindUser(userid) == null)
            {
                throw new UserNotFoundException(userid);
            }


            Tweet tweet = new Tweet(tweetType, userid, message, timestamp);
            //insert into Userline
            TableOperation insertOperation = TableOperation.Insert(new UserLineTweetEntity(tweet));
            _userlineTweet.Execute(insertOperation);

            //insert into QueueMessage
            QueueMessage queueMessage = new QueueMessage(QueueMessage.TypeTweet, tweet.ToJsonString());
            _queue.AddMessage(queueMessage.toAzureCloudQueueMessage());
        }

        public void PostRetweet(string userid, string originTweetUser, string originTweetID, DateTime timestamp)
        {
            if (_accManager.FindUser(userid) == null)
            {
                throw new UserNotFoundException(userid);
            }

            TableOperation retreiveOperation = TableOperation.Retrieve<UserLineTweetEntity>(originTweetUser, originTweetID);
            TableResult retreiveResult = _userlineTweet.Execute(retreiveOperation);
            UserLineTweetEntity originTweet = ((UserLineTweetEntity)retreiveResult.Result);

            if (originTweet == null)
            {
                throw new TweetNotFoundException();
            }

            JObject oTweet = JObject.Parse(originTweet.TweetContent);
            if (Tweet.TweetTypeRetweet.Equals(oTweet["Type"]))
            {
                throw new RetweetARetweetException();
            }

            Tweet tweet = new Tweet(Tweet.TweetTypeRetweet, userid, originTweet.TweetContent, timestamp);
            //insert into Userline
            TableOperation insertOperation = TableOperation.Insert(new UserLineTweetEntity(tweet));
            _userlineTweet.Execute(insertOperation);

            //insert into QueueMessage
            QueueMessage queueMessage = new QueueMessage(QueueMessage.TypeTweet, tweet.ToJsonString());
            _queue.AddMessage(queueMessage.toAzureCloudQueueMessage());

            //update retweet count
            originTweet.RetweetCount++;
            TableOperation updateOperation = TableOperation.Replace(originTweet);
            _userlineTweet.Execute(updateOperation);
        }

        public void SpreadTweet(Tweet tweet)
        {
            List<UserProfile> followers = _accManager.Followers(tweet.User);
            //speed tweet to followers

            //todo: BatchInsert
            foreach (UserProfile user in followers)
            {
                HomeLineTweetEntity entity = new HomeLineTweetEntity(user.Userid, tweet);
                TableOperation insertOperation = TableOperation.Insert(entity);
                _homelineTweet.Execute(insertOperation);
            }
        }


        public void PostReply(  string fromUser, 
                                string toUser, 
                                string content, 
                                DateTime timestamp,
                                string originTweetUser, 
                                string originTweetID)
        {
            if (_accManager.FindUser(fromUser) == null)
            {
                throw new UserNotFoundException(fromUser);
            }
            if (_accManager.FindUser(toUser) == null)
            {
                throw new UserNotFoundException(toUser);
            }
            if (_accManager.FindUser(originTweetUser) == null)
            {
                throw new UserNotFoundException(originTweetUser);
            }

            TableOperation retreiveOperation = TableOperation.Retrieve<UserLineTweetEntity>(originTweetUser, originTweetID);
            TableResult retreiveResult = _userlineTweet.Execute(retreiveOperation);
            UserLineTweetEntity originTweet = ((UserLineTweetEntity)retreiveResult.Result);

            if (originTweet == null)
            {
                throw new TweetNotFoundException();
            }

            Reply reply = new Reply(fromUser, toUser, content, timestamp, originTweetUser, originTweetID);
            //insert reply
            ReplyEntity replyEntity = new ReplyEntity(reply);
            TableOperation insertOperation = TableOperation.Insert(replyEntity);
            _reply.Execute(insertOperation);

            //update reply count
            originTweet.ReplyCount++;
            TableOperation updateOperation = TableOperation.Replace(originTweet);
            _userlineTweet.Execute(updateOperation);

            //notif user as well as the tweet publisher
            ReplyNotificationEntifity notifUserEntity = new ReplyNotificationEntifity(reply);
            TableBatchOperation batchInsert = new TableBatchOperation();
            batchInsert.Insert(notifUserEntity);
            if (!reply.ToUser.Equals(reply.TweetUser))
            {
                ReplyNotificationEntifity notifTweetPublisherEntity = new ReplyNotificationEntifity(reply.TweetUser, reply);
                batchInsert.Insert(notifTweetPublisherEntity);
            }            
            _replyNotification.ExecuteBatch(batchInsert);
        }
    }
}
