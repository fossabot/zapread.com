﻿using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.AspNet.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using zapread.com.Controllers;
using zapread.com.Models;

namespace zapread.com.Tests.Controllers
{
    [TestClass]
    public class MessagesControllerTest
    {
        [TestMethod]
        public void TestMessagesRecentUnreadMessages()
        {
            // Arrange
            MessagesController controller = new MessagesController();

            // Act
            ActionResult result = controller.RecentUnreadMessages(1) as ActionResult;

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void TestMessagesIndex()
        {
            // Arrange
            var context = new Mock<HttpContextBase>();

            var identity = new GenericIdentity("test");
            identity.AddClaim(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "f752739e-8d58-4bf5-a140-fc225cc5ebdb")); //test user
            var principal = new GenericPrincipal(identity, new[] { "user" });
            context.Setup(s => s.User).Returns(principal);
            
            MessagesController controller = new MessagesController();
            controller.ControllerContext = new ControllerContext(context.Object, new RouteData(), controller);

            // Act
            ViewResult result = controller.Index() as ViewResult;

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void TestMessagesChat()
        {
            // Arrange
            var context = new Mock<HttpContextBase>();

            var identity = new GenericIdentity("test");
            identity.AddClaim(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "f752739e-8d58-4bf5-a140-fc225cc5ebdb")); //test user
            var principal = new GenericPrincipal(identity, new[] { "user" });
            context.Setup(s => s.User).Returns(principal);

            MessagesController controller = new MessagesController();
            controller.ControllerContext = new ControllerContext(context.Object, new RouteData(), controller);

            // Act
            ViewResult result = controller.Chat(username: "Zelgada").Result as ViewResult;

            // Assert
            Assert.IsNotNull(result);
        }
    }
}
