﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Web;
using zapread.com.Models;

namespace zapread.com.Database
{
    public class ZapContext : DbContext
    {
        public ZapContext() : base("name=Zapread")
        {

        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserImage> Images { get; set; }
        public DbSet<LNTransaction> LightningTransactions { get; set; }
        public DbSet<EarningEvent> EarningEvents { get; set; }
        public DbSet<SpendingEvent> SpendingEvents { get; set; }
        public DbSet<ZapReadGlobals> ZapreadGlobals { get; set; }
        public DbSet<ZapIcon> Icons { get; set; }

        public DbSet<PendingPostVote> PendingPostVotes { get; set; }
        public DbSet<PendingCommentVote> PendingCommentVotes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }
    }
}