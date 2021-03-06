﻿using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using zapread.com.Database;
using System.Data.Entity;
using zapread.com.Models;
using zapread.com.Helpers;

namespace zapread.com.Controllers
{
    public class VoteController : Controller
    {
        /// <summary>
        /// This is the REST call model
        /// </summary>
        public class Vote
        {
            public int Id { get; set; }
            public int d { get; set; }
            public int a { get; set; }
            public int tx { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> Post(Vote v)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { result = "error", message = "Invalid" });
            }

            if (v.a < 1)
            {
                return Json(new { result = "error", message = "Invalid" });
            }

            var userId = User.Identity.GetUserId();

            // if userId is null, then it is anonymous

            using (var db = new ZapContext())
            {
                var post = db.Posts
                    .Include("VotesUp")
                    .Include("VotesDown")
                    .Include(p => p.UserId)
                    .Include(p => p.UserId.Funds)
                    .FirstOrDefault(p => p.PostId == v.Id);

                if (post == null)
                {
                    return Json(new { result = "error", message = "Invalid Post" });
                }

                Models.User user = null;

                if (userId == null)// Anonymous vote
                { 
                    // Check if vote tx has been claimed
                    if (v.tx != -1337) //debugging secret
                    {
                        var vtx = db.LightningTransactions.FirstOrDefault(tx => tx.Id == v.tx);

                        if (vtx == null || vtx.IsSpent == true)
                        {
                            return Json(new { result = "error", message = "No transaction to vote with" });
                        }

                        vtx.IsSpent = true;
                        await db.SaveChangesAsync();
                    }
                }
                else
                {
                    user = db.Users
                        .Include(usr => usr.Funds)
                        .Include(usr => usr.EarningEvents)
                        .Include(usr => usr.SpendingEvents)
                        .FirstOrDefault(u => u.AppId == userId);

                    if (user == null)
                    {
                        return Json(new { result = "error", message = "Invalid User" });
                    }

                    if (user.Funds.Balance < v.a)
                    {
                        return Json(new { result = "error", message = "Insufficient Funds." });
                    }

                    user.Funds.Balance -= v.a;
                }

                var spendingEvent = new SpendingEvent()
                {
                   Amount = v.a,
                   Post = post,
                   TimeStamp = DateTime.UtcNow,
                };

                double userBalance = 0.0;
                if (user != null)
                {
                    userBalance = user.Funds.Balance;
                    user.SpendingEvents.Add(spendingEvent);
                }

                if (v.d == 1)
                {
                    // Voted up
                    if (user != null && post.VotesUp.Contains(user))
                    {
                        // Already voted - remove upvote?
                        //post.VotesUp.Remove(user);
                        //user.PostVotesUp.Remove(post);
                        //post.Score = post.VotesUp.Count() - post.VotesDown.Count();
                        //return Json(new { result = "success", message = "Already Voted", delta = 0, score = post.Score, balance = user.Funds.Balance });
                    }
                    else if (user != null)
                    {
                        post.VotesUp.Add(user);
                        user.PostVotesUp.Add(post);
                    }

                    //post.VotesDown.Remove(user);
                    //user.PostVotesDown.Remove(post);
                    //post.Score = post.VotesUp.Count() - post.VotesDown.Count();
                    post.Score += v.a;

                    // Record and assign earnings
                    // Related to post owner
                    post.TotalEarned += 0.6*v.a;

                    var ea = new EarningEvent()
                    {
                        Amount = 0.6 * v.a,
                        OriginType = 0,
                        TimeStamp = DateTime.UtcNow,
                        Type = 0,
                    };

                    var webratio = 0.1;
                    var comratio = 0.1;

                    //post.EarningEvents.Add(ea);

                    var owner = post.UserId;
                    if (owner != null)
                    {
                        owner.Reputation += v.a;
                        owner.EarningEvents.Add(ea);
                        owner.TotalEarned += 0.6 * v.a;
                        
                        if (owner.Funds == null)
                        {
                            owner.Funds = new UserFunds() { Balance = 0.6 * v.a, TotalEarned = 0.6 * v.a };
                        }
                        else
                        {
                            owner.Funds.Balance += 0.6 * v.a;
                        }
                    }

                    var postGroup = post.Group;
                    if (postGroup != null)
                    {
                        postGroup.TotalEarnedToDistribute += 0.2 * v.a;
                    }
                    else
                    {
                        // not in group - send to community
                        comratio += 0.2;
                    }

                    var website = db.ZapreadGlobals.FirstOrDefault(i => i.Id == 1);

                    if (website != null)
                    {
                        // Will be distributed to all users
                        website.CommunityEarnedToDistribute += comratio * v.a;

                        // And to the website
                        website.ZapReadTotalEarned += webratio * v.a;
                        website.ZapReadEarnedBalance += webratio * v.a;
                    }

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                        return Json(new { result = "error", message = "Error" });
                    }
                    
                    return Json(new { result = "success", delta = 1, score = post.Score, balance = userBalance, scoreStr = post.Score.ToAbbrString() });
                }
                else
                {
                    // Voted down
                    if (user != null && post.VotesDown.Contains(user))
                    {
                        //post.VotesDown.Remove(user);
                        //user.PostVotesDown.Remove(post);
                        //post.Score = post.VotesUp.Count() - post.VotesDown.Count();
                        //return Json(new { result = "success", message = "Already Voted", delta = 0, score = post.Score, balance = user.Funds.Balance });
                    }
                    else if (user != null)
                    {
                        post.VotesDown.Add(user);
                        user.PostVotesDown.Add(post);
                    }
                    //post.VotesUp.Remove(user);
                    //user.PostVotesUp.Remove(post);

                    post.Score = post.Score - v.a;// post.VotesUp.Count() - post.VotesDown.Count();

                    // Record and assign earnings
                    // Related to post owner
                    var webratio = 0.1;
                    var comratio = 0.1;

                    var owner = post.UserId;
                    if (owner != null)
                    {
                        owner.Reputation -= v.a;
                    }

                    var postGroup = post.Group;
                    if (postGroup != null)
                    {
                        postGroup.TotalEarnedToDistribute += 0.8 * v.a;
                    }
                    else
                    {
                        comratio += 0.8;
                    }

                    var website = db.ZapreadGlobals.FirstOrDefault(i => i.Id == 1);

                    if (website != null)
                    {
                        // Will be distributed to all users
                        website.CommunityEarnedToDistribute += comratio * v.a;

                        // And to the website
                        website.ZapReadTotalEarned += webratio * v.a;
                        website.ZapReadEarnedBalance += webratio * v.a;
                    }

                    await db.SaveChangesAsync();
                    return Json(new { result = "success", delta = -1, score = post.Score, balance = userBalance, scoreStr = post.Score.ToAbbrString() });
                }
            }
            // All paths have returned
        }

        [HttpPost]
        public async Task<ActionResult> Comment(Vote v)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { result = "error", message = "Invalid" });
            }

            if (v.a < 1)
            {
                return Json(new { result = "error", message = "Invalid" });
            }

            var userId = User.Identity.GetUserId();
            using (var db = new ZapContext())
            {
                var comment = db.Comments
                    .Include(c => c.VotesUp)
                    .Include(c => c.VotesDown)
                    .Include(c => c.UserId)
                    .Include(c => c.UserId.Funds)
                    .Include(c => c.Post)
                    .FirstOrDefault(c => c.CommentId == v.Id);

                if (comment == null)
                {
                    return Json(new { result = "error", message = "Invalid Comment" });
                }

                zapread.com.Models.User user = null;

                if (userId == null) // Anonymous vote
                {
                    // Check if vote tx has been claimed
                    if (v.tx != -1337) //debugging secret
                    {
                        var vtx = db.LightningTransactions.FirstOrDefault(tx => tx.Id == v.tx);

                        if (vtx == null || vtx.IsSpent == true)
                        {
                            return Json(new { result = "error", message = "No transaction to vote with" });
                        }

                        vtx.IsSpent = true;
                        await db.SaveChangesAsync();
                    }
                }
                else
                {
                    user = db.Users
                        .Include(usr => usr.Funds)
                        .Include(usr => usr.EarningEvents)
                        .FirstOrDefault(u => u.AppId == userId);

                    if (user == null)
                    {
                        return Json(new { result = "error", message = "Invalid User" });
                    }

                    if (user.Funds.Balance < v.a)
                    {
                        return Json(new { result = "error", message = "Insufficient Funds." });
                    }

                    user.Funds.Balance -= v.a;
                }

                var spendingEvent = new SpendingEvent()
                {
                    Amount = v.a,
                    Comment = comment,
                    TimeStamp = DateTime.UtcNow,
                };

                double userBalance = 0.0;
                if (user != null)
                {
                    userBalance = user.Funds.Balance;
                    user.SpendingEvents.Add(spendingEvent);
                }

                if (v.d == 1)
                {
                    if (comment.VotesUp.Contains(user))
                    {
                        // Already voted
                    }
                    else if (user != null)
                    {
                        comment.VotesUp.Add(user);
                        user.CommentVotesUp.Add(comment);
                    }

                    comment.Score += v.a;
                    comment.TotalEarned += 0.6 * v.a;

                    var ea = new Models.EarningEvent()
                    {
                        Amount = 0.6 * v.a,
                        OriginType = 1, // Comment
                        TimeStamp = DateTime.UtcNow,
                        Type = 0, // Direct earning
                    };

                    var webratio = 0.1;
                    var comratio = 0.1;

                    var owner = comment.UserId;
                    if (owner != null)
                    {
                        owner.Reputation += v.a;
                        owner.EarningEvents.Add(ea);
                        owner.TotalEarned += 0.6 * v.a;
                        if (owner.Funds == null)
                        {
                            owner.Funds = new UserFunds() { Balance = 0.6 * v.a, TotalEarned = 0.6 * v.a };
                        }
                        else
                        {
                            owner.Funds.Balance += 0.6 * v.a;
                        }
                    }

                    if (comment.Post != null)
                    {
                        var group = comment.Post.Group;
                        if (group != null)
                        {
                            group.TotalEarnedToDistribute += 0.2 * v.a;
                        }
                        else
                        {
                            // not in group - send to community
                            comratio += 0.2;
                        }
                    }
                    else
                    {
                        comratio += 0.2;
                    }

                    var website = db.ZapreadGlobals.FirstOrDefault(i => i.Id == 1);

                    if (website != null)
                    {
                        // Will be distributed to all users
                        website.CommunityEarnedToDistribute += comratio * v.a;

                        // And to the website
                        website.ZapReadTotalEarned += webratio * v.a;
                        website.ZapReadEarnedBalance += webratio * v.a;
                    }
                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                        return Json(new { result = "error", message = "Error" });
                    }

                    return Json(new { result = "success", delta = 1, score = comment.Score, balance = userBalance, scoreStr = comment.Score.ToAbbrString() });
                }
                else
                {
                    if (comment.VotesDown.Contains(user))
                    {
                        // Already voted
                    }
                    else if (user != null)
                    {
                        comment.VotesDown.Add(user);
                        user.CommentVotesDown.Add(comment);
                    }
                    comment.Score -= v.a;

                    // Record and assign earnings
                    // Related to post owner
                    var webratio = 0.1;
                    var comratio = 0.1;

                    var owner = comment.UserId;
                    if (owner != null)
                    {
                        owner.Reputation -= v.a;
                    }

                    if (comment.Post != null)
                    {
                        var postGroup = comment.Post.Group;
                        if (postGroup != null)
                        {
                            postGroup.TotalEarnedToDistribute += 0.8 * v.a;
                        }
                        else
                        {
                            comratio += 0.8;
                        }
                    }
                    else
                    {
                        comratio += 0.8;
                    }

                    var website = db.ZapreadGlobals.FirstOrDefault(i => i.Id == 1);

                    if (website != null)
                    {
                        // Will be distributed to all users
                        website.CommunityEarnedToDistribute += comratio * v.a;

                        // And to the website
                        website.ZapReadTotalEarned += webratio * v.a;
                        website.ZapReadEarnedBalance += webratio * v.a;
                    }

                    await db.SaveChangesAsync();
                    return Json(new { result = "success", delta = -1, score = comment.Score, balance = userBalance, scoreStr = comment.Score.ToAbbrString() });
                }
            }
        }
    }
}