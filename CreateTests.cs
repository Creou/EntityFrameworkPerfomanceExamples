using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

namespace CodeFirst
{
	public static class CreateTests
	{
		public static bool CreateNewEveryTime = true;

		public static void Go()
		{
			TvpBlogs(1000);
			TvpPosts(1000);
			UpdatePosts(100000, false);
			GetPostsWhereBlogIdInRange(0, 100);
		}

		private static void GetPostsWhereBlogIdInRange(int first, int second)
		{
			using (var db = new BloggingContext())
			{
				IQueryable<Blog> blogs = null;
				Ext.TimedAction(string.Format("Getting blogs between {0} and {1}", first, second), () =>
				{
					blogs = db.Blogs.Where(b => b.BlogId >= first && b.BlogId <= second);
				});
				Console.WriteLine("Got {0} blogs, now getting posts, press return to continue.", blogs.Count());
				Console.ReadLine();

				ICollection<Post> posts = null;
				Ext.TimedAction("Getting Posts:", () =>
				{
					posts = blogs.SelectMany(b => b.Posts).ToList();
				});
				Console.WriteLine("Got {0} posts. Press return to end.", posts.Count);
				Console.ReadLine();
			}
		}

		private static void CreateBlogs(int num, bool postpone = true)
		{
			var message = postpone ? " with postponed change detection" : "";
			Ext.TimedAction(string.Format("Creating {0} blogs{1}.", num, message), () =>
			{
				using (var db = new BloggingContext())
				{
					db.Configuration.AutoDetectChangesEnabled = !postpone;
					for (int i = 0; i < num; i++)
					{
						db.Blogs.Add(new Blog { Name = "blog " + i.ToString() });
					}
					if (postpone)
					{
						db.ChangeTracker.DetectChanges();
					}
					db.SaveChanges();
				}
			});
			using (var db = new BloggingContext())
			{
				Console.WriteLine("Created {0} blogs", db.Blogs.Count());
			}
		}

		private static void CreatePosts(int num, bool postpone = true)
		{
			var message = postpone ? " with postponed change detection" : "";
			using (var db = new BloggingContext())
			{
				var blogs = db.Blogs.Select(b => new { b.BlogId, b.Name }).ToList();
				Ext.TimedAction(string.Format("Creating {0} posts per blog{1}", num, message),
					() =>
					{
						db.Configuration.AutoDetectChangesEnabled = !postpone;
						foreach (var blog in blogs)
						{
							for (int i = 0; i < num; i++)
							{
								db.Posts.Add(new Post { Title = blog.Name + " Post " + i, BlogId = blog.BlogId, Content = blog.Name + " " + i });
							}
						}
						if (postpone)
						{
							db.ChangeTracker.DetectChanges();
						}
						db.SaveChanges();

					});
			}
			using (var db = new BloggingContext())
			{
				Console.WriteLine("Created {0} blog posts.", db.Posts.Count());
			}
		}

		private static void UpdatePosts(int num, bool postpone = true)
		{
			List<Post> posts = null;
			var message = postpone ? " with postponed change detection" : "";
			using (var db = new BloggingContext())
			{
				db.Configuration.AutoDetectChangesEnabled = !postpone;
				Ext.TimedAction(string.Format("Getting first {0} posts", num),
					() =>
						{
							posts = db.Posts.AsQueryable().Take(num).ToList();
						});
				Ext.TimedAction(string.Format("Updating {0} posts{1}", num, message),
					() =>
					{
						foreach (var post in posts)
						{
							post.Content = post.Content + " alteration";
							post.Title = post.Title + "a";
						}
						if (postpone)
						{
							db.ChangeTracker.DetectChanges();
						}
						db.SaveChanges();

					});
			}
			using (var db = new BloggingContext())
			{
				Console.WriteLine("Updated {0} blog posts.", db.Posts.Count());
			}
		}

		private static void CreateBlogsAddRange(int num)
		{
			List<Blog> blogs = new List<Blog>();
			Ext.TimedAction(
				"Creating blogs in memory",
				() =>
					{
						for (int i = 0; i < num; i++)
						{
							blogs.Add(new Blog { Name = "blog " + i });
						}
					});

			Ext.TimedAction(string.Format("Adding {0} blogs to db with AddRange.", num), () =>
			{
				using (var db = new BloggingContext())
				{
					db.Configuration.AutoDetectChangesEnabled = true; //this is the default anyway.
					db.Blogs.AddRange(blogs);
					db.SaveChanges();
				}
			});
			using (var db = new BloggingContext())
			{
				Console.WriteLine("{0} blogs in db", db.Blogs.Count());
			}
		}

		private static void CreatePostsAddRange(int num)
		{
			List<Blog> blogs = null;
			List <Post> posts = new List<Post>();
			Ext.TimedAction("Getting blogs and creating posts in memory",
				() =>
					{
						using (var db = new BloggingContext())
						{
							blogs = db.Blogs.ToList();
							foreach (var blog in blogs)
							{
								for (int i = 0; i < num; i++)
								{
									posts.Add(new Post { Title = blog.Name + " Post " + i, BlogId = blog.BlogId, Content = blog.Name + " " + i });
								}
							}
						}
					});
			using (var db = new BloggingContext())
			{
				Ext.TimedAction(string.Format("Adding {0} posts per blog to blogs with AddRange", num),
					() =>
					{
						db.Configuration.AutoDetectChangesEnabled = true;
						db.Posts.AddRange(posts);
						db.SaveChanges();

					});
				Console.WriteLine("There are {0} posts.", db.Posts.Count());
			}
		}

		private static void TvpBlogs(int num)
		{
			string conStr = ConfigurationManager.ConnectionStrings["BloggingContext"].ConnectionString;
			
			using (var db = new BloggingContext())
			{
				db.Database.Initialize(true); //because were going to create new every time create here so count is correct.
			}
			DataTable blogsDt = new DataTable("Blogs");
			Ext.TimedAction("Creating new blogs in memory",
				() =>
					{
						blogsDt.Columns.Add("Name", typeof(string));
						for (int i = 0; i < num; i++)
						{
							blogsDt.Rows.Add("blog " + i);
						}
					});
			Ext.TimedAction(
				string.Format("Sending {0} blogs to db via table value parameter storedProcedure.", num),
				() =>
					{
						using (SqlConnection con = new SqlConnection(conStr))
						{
							con.Open();
							SqlCommand sqlCmd = new SqlCommand("dbo.InsertBlogsTVP", con) { CommandType = CommandType.StoredProcedure };
							SqlParameter tvpParam = sqlCmd.Parameters.AddWithValue("@ItemTVP", blogsDt);
							tvpParam.SqlDbType = SqlDbType.Structured;
							sqlCmd.ExecuteNonQuery();
							con.Close();
						}
					});
			using (var db = new BloggingContext())
			{
				Console.WriteLine("Found {0} blogs in db", db.Blogs.Count());
			}
		}

		private static void TvpPosts(int num)
		{
			string conStr = ConfigurationManager.ConnectionStrings["BloggingContext"].ConnectionString;
			
			DataTable postsDt = new DataTable("Posts");

			List<Blog> blogs = null;

			using (var db = new BloggingContext())
			{
				blogs = db.Blogs.AsNoTracking().ToList();
			}

			Ext.TimedAction("Creating posts in memory", () =>
				{
					postsDt.Columns.Add("Title", typeof(string));
					postsDt.Columns.Add("Content", typeof(string));
					postsDt.Columns.Add("BlogId", typeof(int));
					foreach (var blog in blogs)
					{
						for (int i = 0; i < num; i++)
						{
							postsDt.Rows.Add("post " + i, blog.Name + " " + i, blog.BlogId);
						}
					}
				});
			Ext.TimedAction(string.Format("Sending {0} posts to db via table value parameter storedProcedure.", postsDt.Rows.Count), () =>
				{
					using (SqlConnection con = new SqlConnection(conStr))
					{
						con.Open();
						SqlCommand sqlCmd = new SqlCommand("dbo.InsertPostsTVP", con)
						{
							CommandType = CommandType.StoredProcedure
						};
						SqlParameter tvpParam = sqlCmd.Parameters.AddWithValue("@ItemTVP", postsDt);
						tvpParam.SqlDbType = SqlDbType.Structured;
						sqlCmd.ExecuteNonQuery();
						con.Close();
					}
			});
			using (var db = new BloggingContext())
			{
				Console.WriteLine("Found {0} posts", db.Posts.Count());
			}
		}
	}

	public class Blog
	{
		public int BlogId { get; set; }
		public string Name { get; set; }
		public virtual List<Post> Posts { get; set; }
	}

	public class Post
	{
		public int PostId { get; set; }
		public string Title { get; set; }
		public string Content { get; set; }
		public int BlogId { get; set; }
		public virtual Blog Blog { get; set; }
	}

	public class BloggingContext : DbContext
	{
		public DbSet<Blog> Blogs { get; set; }
		public DbSet<Post> Posts { get; set; }

		public BloggingContext()
		{
			if (CreateTests.CreateNewEveryTime)
			{
				Database.SetInitializer(new InitBlogDb());
			}
		}
	}

	public class InitBlogDb : DropCreateDatabaseAlways<BloggingContext>
	{
		protected override void Seed(BloggingContext context)
		{
			base.Seed(context);
			context.Database.ExecuteSqlCommand(
			@"
				CREATE TYPE [dbo].[TVP_PostItems] AS TABLE(
				 [Title] [nvarchar](50) NULL,
				 Content nvarchar(50) NULL,
				 BlogId int
				)
			");
			context.Database.ExecuteSqlCommand(
			@"
				CREATE TYPE [dbo].[TVP_BlogItems] AS TABLE(
				 [Name] [nvarchar](50) NULL
				)
			");
			context.Database.ExecuteSqlCommand(
			@"
				CREATE PROCEDURE [dbo].[InsertBlogsTVP]
				 @ItemTVP TVP_BlogItems READONLY
				AS
				BEGIN
				 INSERT INTO dbo.Blogs (Name)
				 SELECT Name
				 FROM @ItemTVP
				END
			");
			context.Database.ExecuteSqlCommand(
			@"
				CREATE PROCEDURE [dbo].[InsertPostsTVP]
				 @ItemTVP TVP_PostItems READONLY
				AS
				BEGIN
				 INSERT INTO dbo.Posts (Title, Content, BlogId)
				 SELECT Title, Content, BlogId
				 FROM @ItemTVP
				END
			");
		}
	}
}
