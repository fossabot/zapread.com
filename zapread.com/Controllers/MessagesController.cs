﻿using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using zapread.com.Database;
using zapread.com.Models;
using zapread.com.Services;

namespace zapread.com.Controllers
{
    public class MessagesController : Controller
    {
        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        // GET: Messages
        public ActionResult Index()
        {
            var userId = User.Identity.GetUserId();
            if (userId == null)
            {
                return RedirectToAction("Index", "Home");
            }

            using (var db = new ZapContext())
            {
                var user = db.Users
                    .Include("Alerts")
                    .Include("Messages")
                    .Include("Alerts.PostLink")
                    .Include("Messages.PostLink")
                    .Where(u => u.AppId == userId).First();

                var messages = user.Messages.Where(m => !m.IsRead && !m.IsDeleted).ToList();

                var alerts = user.Alerts.Where(m => !m.IsRead && !m.IsDeleted).ToList();

                var vm = new MessagesViewModel()
                {
                    Messages = messages,
                    Alerts = alerts,
                };

                return View(vm);
            }
        }

        [Route("Messages/Chat/{username?}")]
        public async Task<ActionResult> Chat(string username)
        {
            var userId = User.Identity.GetUserId();
            if (userId == null)
            {
                return RedirectToAction("Index", "Home");
            }
            using (var db = new ZapContext())
            {
                var vm = new ChatMessagesViewModel();

                var user = db.Users
                    .Include("Messages")
                    .Include("Messages.PostLink")
                    .Include("Messages.From")
                    .Where(u => u.AppId == userId).FirstOrDefault();

                var otheruser = db.Users
                    .Include("Messages")
                    .Include("Messages.PostLink")
                    .Include("Messages.From")
                    .Where(u => u.Name == username).FirstOrDefault();

                if (otheruser == null)
                {
                    return RedirectToAction("Index", "Messages");
                }

                int thisUserId = user.Id;
                int otherUserId = otheruser.Id;

                // Better to just search from & to?

                var receivedMessages = user.Messages.Where(m => m.From != null && m.From.Id == otherUserId).Where(m => !m.IsDeleted).Where(m => m.Title.StartsWith("Private")).ToList();
                var sentMessages = otheruser.Messages.Where(m => m.From != null && m.From.Id == thisUserId).Where(m => !m.IsDeleted).Where(m => m.Title.StartsWith("Private")).ToList();

                var messages = new List<ChatMessageViewModel>();

                foreach (var m in receivedMessages)
                {
                    messages.Add(new ChatMessageViewModel()
                    {
                        Message = m,
                        From = otheruser,
                        To = user,
                        IsReceived = true,
                    });
                }

                foreach (var m in sentMessages)
                {
                    messages.Add(new ChatMessageViewModel()
                    {
                        Message = m,
                        From = user,
                        To = otheruser,
                        IsReceived = false,
                    });
                }

                vm.Messages = messages.OrderBy(mv => mv.Message.TimeStamp).ToList();

                return View(vm);
            }
        }

        public PartialViewResult UnreadMessages()
        {
            var userId = User.Identity.GetUserId();
            var vm = new UnreadModel();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var user = db.Users
                        //.Include("Alerts")
                        .Include("Messages")
                        //.Include("Alerts.PostLink")
                        .Include("Messages.PostLink")
                        .Where(u => u.AppId == userId).First();

                    var messages = user.Messages.Where(m => !m.IsRead && !m.IsDeleted).OrderByDescending(m => m.TimeStamp);

                    vm.NumUnread = messages.Count();
                }
            }
            return PartialView("_UnreadMessages", model: vm);
        }

        public PartialViewResult RecentUnreadAlerts(int count)
        {
            string userId = null;
            if (User != null)
            {
                userId = User.Identity.GetUserId();
            }
            using (var db = new ZapContext())
            {
                var user = db.Users
                    .Include("Alerts")
                    //.Include("Messages")
                    .Include("Alerts.PostLink")
                    //.Include("Messages.PostLink")
                    //.Include("Messages.From")
                    .Where(u => u.AppId == userId).FirstOrDefault();

                var vm = new RecentUnreadAlertsViewModel();

                if (user != null)
                {
                    var alerts = user.Alerts.Where(m => !m.IsRead && !m.IsDeleted).OrderByDescending(m => m.TimeStamp).Take(count);
                    vm.Alerts = alerts.ToList();
                }

                return PartialView("_PartialRecentUnreadAlerts", model: vm);
            }
        }

        public PartialViewResult RecentUnreadMessages(int count)
        {
            string userId = null;
            if (User != null)
            {
                userId = User.Identity.GetUserId();
            }
            using (var db = new ZapContext())
            {
                var user = db.Users
                    //.Include("Alerts")
                    .Include("Messages")
                    //.Include("Alerts.PostLink")
                    .Include("Messages.PostLink")
                    .Include("Messages.From")
                    .Where(u => u.AppId == userId).FirstOrDefault();

                var vm = new RecentUnreadMessagesViewModel();

                if (user != null)
                {
                    var messages = user.Messages.Where(m => !m.IsRead && !m.IsDeleted).OrderByDescending(m => m.TimeStamp).Take(count);
                    vm.Messages = messages.ToList();
                }

                return PartialView("_PartialRecentUnreadMessages", model: vm);
            }
        }

        public PartialViewResult UnreadAlerts()
        {
            string userId = null;
            if (User != null)
            {
                userId = User.Identity.GetUserId();
            }

            var vm = new UnreadModel();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var user = db.Users
                        .Include("Alerts")
                        //.Include("Messages")
                        //.Include("Alerts.PostLink")
                        //.Include("Messages.PostLink")
                        .Where(u => u.AppId == userId).First();

                    var messages = user.Alerts.Where(m => !m.IsRead && !m.IsDeleted).OrderByDescending(m => m.TimeStamp);

                    vm.NumUnread = messages.Count();
                }
            }
            return PartialView("_UnreadMessages", model: vm);
        }

        public async Task<JsonResult> DismissAlert(int id)
        {
            var userId = User.Identity.GetUserId();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var user = db.Users
                        .Include("Alerts")
                        .Where(u => u.AppId == userId).First();

                    if (user == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    if (id == -1)
                    {
                        // dismissed all
                        foreach(var a in user.Alerts.Where(m => !m.IsDeleted && !m.IsRead))
                        {
                            a.IsRead = true;
                        }
                        await db.SaveChangesAsync();
                        return Json(new { Result = "Success" });
                    }

                    var alert = user.Alerts.Where(m => m.Id == id).FirstOrDefault();

                    if (alert == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    alert.IsRead = true;
                    await db.SaveChangesAsync();
                    return Json(new { Result = "Success" });
                }
            }

            return Json(new { Result = "Failure" });
        }

        public async Task<JsonResult> DismissMessage(int id)
        {
            var userId = User.Identity.GetUserId();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var user = db.Users
                        .Include("Messages")
                        .Where(u => u.AppId == userId).First();

                    if (user == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    if (id == -1)
                    {
                        // dismissed all
                        foreach (var a in user.Messages.Where(m => !m.IsDeleted && !m.IsRead))
                        {
                            a.IsRead = true;
                        }
                        await db.SaveChangesAsync();
                        return Json(new { Result = "Success" });
                    }

                    var msg = user.Messages.Where(m => m.Id == id).FirstOrDefault();

                    if (msg == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    msg.IsRead = true;
                    await db.SaveChangesAsync();
                    return Json(new { Result = "Success" });
                }
            }

            return Json(new { Result = "Failure" });
        }

        public async Task<JsonResult> DeleteAlert(int id)
        {
            var userId = User.Identity.GetUserId();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var user = db.Users
                        .Include("Alerts")
                        .Where(u => u.AppId == userId).First();

                    if (user == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    if (id == -1)
                    {
                        // dismissed all
                        foreach (var a in user.Alerts.Where(m => !m.IsDeleted && !m.IsRead))
                        {
                            a.IsDeleted = true;
                        }
                        await db.SaveChangesAsync();
                        return Json(new { Result = "Success" });
                    }

                    var alert = user.Alerts.Where(m => m.Id == id).FirstOrDefault();

                    if (alert == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    alert.IsDeleted = true;
                    await db.SaveChangesAsync();
                    return Json(new { Result = "Success" });
                }
            }

            return Json(new { Result = "Failure" });
        }

        public async Task<JsonResult> DeleteMessage(int id)
        {
            var userId = User.Identity.GetUserId();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var user = db.Users
                        .Include("Messages")
                        .Where(u => u.AppId == userId).First();

                    if (user == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    if (id == -1)
                    {
                        // dismissed all
                        foreach (var a in user.Messages.Where(m => !m.IsDeleted && !m.IsRead))
                        {
                            a.IsDeleted = true;
                        }
                        await db.SaveChangesAsync();
                        return Json(new { Result = "Success" });
                    }

                    var msg = user.Messages.Where(m => m.Id == id).FirstOrDefault();

                    if (msg == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    msg.IsDeleted = true;
                    await db.SaveChangesAsync();
                    return Json(new { Result = "Success" });
                }
            }

            return Json(new { Result = "Failure" });
        }

        public async Task<JsonResult> SendMessage(int id, string content)
        {
            var userId = User.Identity.GetUserId();
            if (userId != null)
            {
                using (var db = new ZapContext())
                {
                    var sender = db.Users
                        .Where(u => u.AppId == userId).FirstOrDefault();

                    var receiver = db.Users
                        .Include("Messages")
                        .Where(u => u.Id == id).FirstOrDefault();

                    if (sender == null)
                    {
                        return Json(new { Result = "Failure" });
                    }

                    var msg = new UserMessage()
                    {
                        Content = content,
                        From = sender,
                        To = receiver,
                        IsDeleted = false,
                        IsRead = false,
                        TimeStamp = DateTime.UtcNow,
                        Title = "Private message from <a href='" + @Url.Action(actionName: "Index", controllerName: "User", routeValues: new { username = sender.Name }) + "'>" + sender.Name + "</a>",//" + sender.Name,
                    };

                    receiver.Messages.Add(msg);
                    await db.SaveChangesAsync();

                    // Send email
                    if (receiver.Settings == null)
                    {
                        receiver.Settings = new UserSettings();
                    }

                    if (receiver.Settings.NotifyOnPrivateMessage)
                    {
                        string mentionedEmail = UserManager.FindById(receiver.AppId).Email;
                        MailingService.Send(user: "Notify",
                            message: new UserEmailModel()
                            {
                                Subject = "New private message",
                                Body = "From: " + sender.Name + "<br/> " + content + "<br/><br/><a href='http://www.zapread.com'>zapread.com</a>",
                                Destination = mentionedEmail,
                                Email = "",
                                Name = "ZapRead.com Notify"
                            });
                    }
                    
                    return Json(new { Result = "Success" });
                }
            }

            return Json(new { Result = "Failure" });
        }
    }
}