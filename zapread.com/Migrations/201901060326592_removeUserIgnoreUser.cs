namespace zapread.com.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class removeUserIgnoreUser : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.User", "UserIgnores_Id", "dbo.UserIgnoreUser");
            DropIndex("dbo.User", new[] { "UserIgnores_Id" });
            DropColumn("dbo.User", "UserIgnores_Id");
            DropTable("dbo.UserIgnoreUser");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.UserIgnoreUser",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.User", "UserIgnores_Id", c => c.Int());
            CreateIndex("dbo.User", "UserIgnores_Id");
            AddForeignKey("dbo.User", "UserIgnores_Id", "dbo.UserIgnoreUser", "Id");
        }
    }
}
