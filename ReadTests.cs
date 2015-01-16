using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;

namespace CodeFirst
{
	public static class ReadTests
	{
		public static bool CreateNewDb = true;

		public static void Go()
		{
			InitLogisticsDb();
			var manusToLookAt = Enumerable.Range(100,100).ToList();
			Dictionary<string, bool> manusHaveSli = null;
			ICollection<string> manus = null;
			
			Ext.TimedAction("\nloop",
				() =>
					{
						manus = GetManufacturersWithSmallLightItems(manusToLookAt);
					}, 10);
			Console.WriteLine("Got {0} manufacturers", manus.Count());

			Ext.TimedAction("\nloop include",
				() =>
					{
						manus = GetManufacturersWithSmallLightItemsInclude(manusToLookAt);
					}, 10);
			Console.WriteLine("Got {0} manufacturers", manus.Count());
			
			Ext.TimedAction("\nlogic in query",
				() =>
					{
						manus = GetManufacturersWithSmallLightItemsInQuery(manusToLookAt);
					}, 10);
			Console.WriteLine("Got {0} manufacturers", manus.Count());
			
			Ext.TimedAction("\nUsing repository",
				() =>
					{
						manus = UsingRepoWithNoTracking(manusToLookAt);
					}, 10);
			Console.WriteLine("Got {0} manufacturers", manus.Count());

			Ext.TimedAction("\nManus have SLI loop",
				() =>
					{
						manusHaveSli = ManufacturersHaveWithSmallLightItemsLoopInclude();
					}, 10);
			Console.WriteLine("Got {0} manufacturers with {1} matching", manusHaveSli.Count(), manusHaveSli.Count(m => m.Value));
			
			Ext.TimedAction("\nManus have SLI in query",
				() =>
					{
						manusHaveSli = ManufacturersHaveWithSmallLightItemsInQuery();
					}, 10);
			Console.WriteLine("Got {0} manufacturers with {1} matching", manusHaveSli.Count(), manusHaveSli.Count(m => m.Value));
			
			Ext.TimedAction("\nManus have SLI in query with AsNoTracking",
				() =>
					{
						manusHaveSli = ManufacturersHaveWithSmallLightItemsInQueryNoTracking();
					}, 10);
			Console.WriteLine("Got {0} manufacturers with {1} matching", manusHaveSli.Count(), manusHaveSli.Count(m => m.Value));

			Console.ReadLine();
		}

		public static ICollection<string> GetManufacturersWithSmallLightItems(ICollection<int> manufacturersToLookAt)
		{
			using (var db = new LogisticsContext())
			{
				List<Manufacturer> manufacturers = db.Manufacturers.Where(m => manufacturersToLookAt.Contains(m.Id)).ToList();
				List<string> manufacturersWithSmallLightItems = new List<string>();
				foreach (var manufacturer in manufacturers)
				{
					if (manufacturer.Items.Any(m => m.MetersCubed < 2 && m.Weight < 5))
					{
						manufacturersWithSmallLightItems.Add(manufacturer.Name);
					}
				}
				return manufacturersWithSmallLightItems;
			}
		}

		public static ICollection<string> GetManufacturersWithSmallLightItemsInclude(ICollection<int> manufacturersToLookAt)
		{
			using (var db = new LogisticsContext())
			{
				List<Manufacturer> manufacturers = db.Manufacturers.Include(i => i.Items).Where(m => manufacturersToLookAt.Contains(m.Id)).ToList();
				List<string> manufacturersWithSmallLightItems = new List<string>();
				foreach (var manufacturer in manufacturers)
				{
					if (manufacturer.Items.Any(m => m.MetersCubed < 2 && m.Weight < 5))
					{
						manufacturersWithSmallLightItems.Add(manufacturer.Name);
					}
				}
				return manufacturersWithSmallLightItems;
			}
		}

		public static ICollection<string> GetManufacturersWithSmallLightItemsInQuery(ICollection<int> manufacturersToLookAt)
		{
			using (var db = new LogisticsContext())
			{
				List<string> manufacturersWithSmallLightItems = db.Manufacturers
					.Where(m => manufacturersToLookAt.Contains(m.Id) && m.Items.Any(i => i.MetersCubed < 2 && i.Weight < 5))
					.Select(m => m.Name)
					.AsNoTracking()
					.ToList();
				
				return manufacturersWithSmallLightItems;
			}
		}
		
		public static Dictionary<string, bool> ManufacturersHaveWithSmallLightItemsLoopInclude()
		{
			using (var db = new LogisticsContext())
			{
				List<Manufacturer> manufacturers = db.Manufacturers.Include(i => i.Items).ToList();
				Dictionary<string, bool> manufacturersHaveSmallLightItems = new Dictionary<string, bool>();
				foreach (var manufacturer in manufacturers)
				{
					manufacturersHaveSmallLightItems.Add(
						manufacturer.Name,
						manufacturer.Items.Any(m => m.MetersCubed < 2 && m.Weight < 5));
				}
				return manufacturersHaveSmallLightItems;
			}
		}

		public static Dictionary<string, bool> ManufacturersHaveWithSmallLightItemsInQuery()
		{
			using (var db = new LogisticsContext())
			{
				var manufacturersWithSmallLightItems = db.Manufacturers
					.Select(m => new {m.Name, hasSLI = m.Items.Any(i => i.MetersCubed < 2 && i.Weight < 5)})
					.ToDictionary(k => k.Name, v=> v.hasSLI);
				
				return manufacturersWithSmallLightItems;
			}
		}

		public static Dictionary<string, bool> ManufacturersHaveWithSmallLightItemsInQueryNoTracking()
		{
			using (var db = new LogisticsContext())
			{
				var manufacturersWithSmallLightItems = db.Manufacturers
					.Select(m => new {m.Name, hasSLI = m.Items.Any(i => i.MetersCubed < 2 && i.Weight < 5)})
					.AsNoTracking()
					.ToDictionary(k => k.Name, v=> v.hasSLI);
				
				return manufacturersWithSmallLightItems;
			}
		}

		public static ICollection<string> ManufacturersWithOutstandingOrders()
		{
			var orderRepo = new LogisticsRepo<ItemOrder>();
			var itemRepo = new LogisticsRepo<Item>();
			var outstandingOrderItemIds = orderRepo.GetQueryable(o => !o.DeliveredDate.HasValue).Select(o => o.ItemId);
			var manus =
				itemRepo.GetQueryable(i => outstandingOrderItemIds.Contains(i.Id))
				        .Select(i => i.Manufacturer.Name)
				        .Distinct()
				        .ToList();
			return manus;
		}

		public static ICollection<string> UsingRepoWithNoTracking(ICollection<int> manufacturersToLookAt)
		{
			var manuRepo = new LogisticsRepo<Manufacturer>();
			

			List<string> manufacturersWithSmallLightItems = manuRepo
				.GetQueryable(m => manufacturersToLookAt.Contains(m.Id) && m.Items.Any(i => i.MetersCubed < 2 && i.Weight < 5))
				.Select(m => m.Name)
				.ToList();
				
			return manufacturersWithSmallLightItems;
			
		}

		public static void InitLogisticsDb()
		{
			using (var db = new LogisticsContext())
			{
				db.Database.Delete();
				db.Database.Initialize(true);
			}
		}
	}


	public class Manufacturer : EfObject
	{
		public string Name { get; set; }
		public bool Preferred { get; set; }
		public virtual List<Item> Items { get; set; }
	}

	public class Item : EfObject
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public int Weight { get; set; }
		public int MetersCubed { get; set; }
		public int ManufacturerId { get; set; }
		public Warehouse DefaultWarehouse { get; set; }
		public virtual Manufacturer Manufacturer { get; set; }
	}

	public class Warehouse : EfObject
	{
		public string Name { get; set; }
	}

	public class ItemOrder : EfObject
	{
		public int ItemId { get; set; }
		public int Quantity { get; set; }
		public DateTime OrderDate { get; set; }
		public DateTime? DeliveredDate { get; set; }
		public int? QuantityDelivered { get; set; }
	}

	public class LogisticsContext : DbContext
	{
		public DbSet<Manufacturer> Manufacturers { get; set; }
		public DbSet<Item> Items { get; set; }
		public DbSet<Warehouse> Warehouses { get; set; }
		public DbSet<ItemOrder> ItemOrders { get; set; }

		public LogisticsContext()
		{
			Database.SetInitializer(new InitLogisticDb());
		}
	}

	public class EfObject
	{
		public int Id { get; set; }
	}

	public class LogisticsRepo<T> where T : EfObject
	{
		protected readonly LogisticsContext _context;

		public LogisticsRepo()
		{
			_context = new LogisticsContext();
		}

		public IQueryable<T> GetQueryable(Expression<Func<T, bool>> query, params Expression<Func<T, object>>[] navigationProperties)
		{
			IQueryable<T> qr = _context.Set<T>();

			foreach (var include in navigationProperties)
			{
				qr = qr.Include(include);
			}

			return qr.AsNoTracking().Where(query); //disconnects objects
		}

		//Doesnt update child objects
		public ICollection<int> UpdateOrCreate(ICollection<T> objects)
		{
			var objectIds = objects.Select(o => o.Id).ToList();

			foreach (var obj in objects)
			{
				_context.Entry(obj).State = EntityState.Modified; //reconnect objects
			}

			_context.SaveChanges();
			return objectIds;
		}
	}


	public class InitLogisticDb : CreateDatabaseIfNotExists<LogisticsContext>
	{
		protected override void Seed(LogisticsContext context)
		{
			const int NumManus = 300;
			const int NumItemsPerManu = 1000;

			var rnd = new Random(1);
			List<Warehouse> warehouses = new List<Warehouse>();
			warehouses.AddRange(Enumerable.Range(1, 10).Select(r => new Warehouse { Name = "Warehouse" + r, Id = r }));
			context.Warehouses.AddRange(warehouses);
			context.SaveChanges();

			warehouses = context.Warehouses.ToList();

			List<Manufacturer> manufacturers = new List<Manufacturer>();
			
			for (int i = 1; i <= NumManus; i++)
			{
				var preferred = Convert.ToBoolean(rnd.Next(0, 2));
				var manu = new Manufacturer
				{
					Name = "Manufacturer" + i.ToString(),
					Preferred = preferred
				};
				manu.Items = new List<Item>();
				for (int c = 1; c <= NumItemsPerManu; c++)
				{
					int whId = rnd.Next(1, 10);
					var wh = warehouses.FirstOrDefault(w => w.Id == whId);
					if (wh == null)
					{
						throw new InvalidProgramException();
					}
					manu.Items.Add(new Item
					{
						Name = "Manu" + i + "Item" + c,
						Description = "Description " + i + ":" + c,
						MetersCubed = rnd.Next(0,100),
						Weight = rnd.Next(0,30),
						DefaultWarehouse = wh
					});
				}
				manufacturers.Add(manu);
			}

			context.Manufacturers.AddRange(manufacturers);
			context.SaveChanges();
		}
	}
}
