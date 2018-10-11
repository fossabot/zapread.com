﻿using LightningLib.lndrpc;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using zapread.com.Database;
using zapread.com.Hubs;
using zapread.com.Models;
using System.Data.Entity;
using Microsoft.AspNet.Identity;
using System.Threading;
using zapread.com.Services;
using System.Net;

namespace zapread.com.Controllers
{
    public class LightningController : Controller
    {
        /// <summary>
        /// This is the interface to a singleton payments service which is injected for IOC.
        /// </summary>
        public ILightningPayments paymentsService { get; private set; }

        /// <summary>
        /// Constructor with dependency injection for IOC and controller singleton control.
        /// </summary>
        /// <param name="paymentsService"></param>
        public LightningController(ILightningPayments paymentsService)
        {
            this.paymentsService = paymentsService;
        }

        private static ConcurrentDictionary<Guid, TransactionListener> lndTransactionListeners = new ConcurrentDictionary<Guid, TransactionListener>();

        //Used for rate limiting double withdraws
        static ConcurrentDictionary<string, DateTime> WithdrawRequests = new ConcurrentDictionary<string, DateTime>();

        // GET: Lightning
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult GetDepositInvoice(string amount, string memo, string anon)
        {
            bool isAnon = !(anon == null || anon != "1");
            if (!isAnon && !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.ToString() });
            }

            string userId;
            if (isAnon)
            {
                userId = null;
            }
            else
            {
                userId = User.Identity.GetUserId();
            }

            if (memo == null || memo == "")
            {
                memo = "Zapread.com";
            }

            var lndClient = new LndRpcClient(
                    host: System.Configuration.ConfigurationManager.AppSettings["LnMainnetHost"],
                    macaroonAdmin: System.Configuration.ConfigurationManager.AppSettings["LnMainnetMacaroonAdmin"],
                    macaroonRead: System.Configuration.ConfigurationManager.AppSettings["LnMainnetMacaroonRead"],
                    macaroonInvoice: System.Configuration.ConfigurationManager.AppSettings["LnMainnetMacaroonInvoice"]);

            var inv = lndClient.AddInvoice(Convert.ToInt64(amount), memo: memo, expiry: "432000");

            LnRequestInvoiceResponse resp = new LnRequestInvoiceResponse()
            {
                Invoice = inv.payment_request,
                Result = "success",
            };

            //Create transaction record (not settled)

            using (ZapContext db = new ZapContext())
            {
                // TODO: ensure user exists?
                zapread.com.Models.User user = null;
                if (userId != null)
                {
                    user = db.Users.Where(u => u.AppId == userId).First();
                }

                //create a new transaction
                LNTransaction t = new LNTransaction()
                {
                    User = user,
                    IsSettled = false,
                    IsSpent = false,
                    Memo = memo,
                    Amount = Convert.ToInt64(amount),
                    HashStr = inv.r_hash,
                    IsDeposit = true,
                    TimestampCreated = DateTime.Now,
                    PaymentRequest = inv.payment_request,
                };
                db.LightningTransactions.Add(t);
                db.SaveChanges();
            }

            // If a listener is not already running, this should start

            // Check if there is one already online.
            var numListeners = lndTransactionListeners.Count(kvp => kvp.Value.IsLive);

            // If we don't have one running - start it and subscribe
            if (numListeners < 1)
            {
                var listener = lndClient.GetListener();
                lndTransactionListeners.TryAdd(listener.ListenerId, listener);           //keep alive while we wait for payment
                listener.InvoicePaid += NotifyClientsInvoicePaid;     //handle payment message
                listener.StreamLost += OnListenerLost;                  //stream lost
                var a = new Task(() => listener.Start());                   //listen for payment
                a.Start();
            }
            return Json(resp);
        }

        private static void OnListenerLost(TransactionListener l)
        {
            lndTransactionListeners.TryRemove(l.ListenerId, out TransactionListener oldListener);
        }

        // We have received asynchronous notification that a lightning invoice has been paid
        private static void NotifyClientsInvoicePaid(Invoice invoice)
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<NotificationHub>();

            //Save in db
            using (ZapContext db = new ZapContext())
            {
                //check if unsettled transaction exists
                var tx = db.LightningTransactions
                    .Include(tr => tr.User)
                    .Where(tr => tr.PaymentRequest == invoice.payment_request)
                    .ToList();
                if (invoice.settled)
                {
                    DateTime settletime = DateTime.UtcNow;

                    LNTransaction t;
                    if (tx.Count > 0)
                    {
                        t = tx.First();
                        t.TimestampSettled = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc) + TimeSpan.FromSeconds(Convert.ToInt64(invoice.settle_date));
                        t.IsSettled = true;
                    }
                    else
                    {
                        // This invoice is not related to this service.
                        settletime = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc) + TimeSpan.FromSeconds(Convert.ToInt64(invoice.settle_date));
                        t = new LNTransaction()
                        {
                            IsSettled = invoice.settled,
                            Memo = invoice.memo,
                            Amount = Convert.ToInt64(invoice.value),
                            HashStr = invoice.r_hash,
                            IsDeposit = true,
                            TimestampSettled = settletime,
                            TimestampCreated = DateTime.SpecifyKind(new DateTime(1970, 1, 1), DateTimeKind.Utc) + TimeSpan.FromSeconds(Convert.ToInt64(invoice.creation_date)),
                            PaymentRequest = invoice.payment_request,
                            User = null,
                        };
                        db.LightningTransactions.Add(t);
                    }

                    var user = t.User;
                    double userBalance = 0.0;

                    if (user == null)
                    {

                    }
                    else
                    {
                        user.Funds.Balance += Convert.ToInt64(invoice.value);
                        userBalance = Math.Floor(user.Funds.Balance);
                    }
                    t.IsSettled = invoice.settled;
                    db.SaveChanges();

                    context.Clients.All.NotifyInvoicePaid(new { invoice = invoice.payment_request, balance = userBalance, txid = t.Id });
                }
                
            }
        }

        [HttpPost]
        public ActionResult SubmitPaymentRequest(string request)
        {
            var userId = User.Identity.GetUserId();

            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.ToString() });
            }

            var lndClient = new LndRpcClient(
                    host: System.Configuration.ConfigurationManager.AppSettings["LnMainnetHost"],
                    macaroonAdmin: System.Configuration.ConfigurationManager.AppSettings["LnMainnetMacaroonAdmin"],
                    macaroonRead: System.Configuration.ConfigurationManager.AppSettings["LnMainnetMacaroonRead"],
                    macaroonInvoice: System.Configuration.ConfigurationManager.AppSettings["LnMainnetMacaroonInvoice"]);

            string ip = GetClientIpAddress(Request);

            try
            {
                var paymentResult = paymentsService.TryWithdrawal(request, userId, ip, lndClient);
                return Json(paymentResult);
            }
            catch (Exception e)
            {
                MailingService.Send(new UserEmailModel() {
                    Destination = "steven.horn.mail@gmail.com",
                    Body = " Exception: " + e.Message + "\r\n Stack: " + e.StackTrace + "\r\n invoice: " + request + "\r\n user: " + userId,
                    Email = "",
                    Name = "zapread.com Exception",
                    Subject = "User withdraw error",
                });
                return Json(new { Result = "Error processing request." });
            }
        }

        public static string GetClientIpAddress(HttpRequestBase request)
        {
            try
            {
                var userHostAddress = request.UserHostAddress;

                // Attempt to parse.  If it fails, we catch below and return "0.0.0.0"
                // Could use TryParse instead, but I wanted to catch all exceptions
                IPAddress.Parse(userHostAddress);

                var xForwardedFor = request.ServerVariables["X_FORWARDED_FOR"];

                if (string.IsNullOrEmpty(xForwardedFor))
                    return userHostAddress;

                // Get a list of public ip addresses in the X_FORWARDED_FOR variable
                var publicForwardingIps = xForwardedFor.Split(',').Where(ip => !IsPrivateIpAddress(ip)).ToList();

                // If we found any, return the last one, otherwise return the user host address

                var retval = publicForwardingIps.Any() ? publicForwardingIps.Last() : userHostAddress;

                return retval;
            }
            catch (Exception)
            {
                // Always return all zeroes for any failure (my calling code expects it)
                return "0.0.0.0";
            }
        }

        private static bool IsPrivateIpAddress(string ipAddress)
        {
            // http://en.wikipedia.org/wiki/Private_network
            // Private IP Addresses are: 
            //  24-bit block: 10.0.0.0 through 10.255.255.255
            //  20-bit block: 172.16.0.0 through 172.31.255.255
            //  16-bit block: 192.168.0.0 through 192.168.255.255
            //  Link-local addresses: 169.254.0.0 through 169.254.255.255 (http://en.wikipedia.org/wiki/Link-local_address)

            var ip = IPAddress.Parse(ipAddress);
            var octets = ip.GetAddressBytes();

            var is24BitBlock = octets[0] == 10;
            if (is24BitBlock) return true; // Return to prevent further processing

            var is20BitBlock = octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31;
            if (is20BitBlock) return true; // Return to prevent further processing

            var is16BitBlock = octets[0] == 192 && octets[1] == 168;
            if (is16BitBlock) return true; // Return to prevent further processing

            var isLinkLocalAddress = octets[0] == 169 && octets[1] == 254;
            return isLinkLocalAddress;
        }
    }
}