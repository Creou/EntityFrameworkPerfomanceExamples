using System;

namespace CodeFirst
{
	public static class Ext
	{
		public static void TimedAction(string startMessage, Action action, int repeat = 1)
		{
			Console.WriteLine(startMessage);
			if (repeat > 1)
			{
				action(); //minimise cache issues
			}

			var startTime = DateTime.Now;
			for (int i = 0; i < repeat; i++)
			{
				action();
			}
			var endTime = DateTime.Now;
			var ticks = endTime.Subtract(startTime).Ticks;
			ticks = ticks / repeat;
			var length = new TimeSpan(ticks);
			Console.WriteLine("h:{0}, m:{1}, s:{2}, ms:{3}", length.Hours, length.Minutes, length.Seconds, length.Milliseconds);
		}
	}
}