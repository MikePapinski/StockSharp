namespace SampleSmartConsole
{
	using System;
	using System.Linq;
	using System.Threading;

	using Ecng.Common;
	using Ecng.Localization;

	using StockSharp.BusinessEntities;
	using StockSharp.Messages;
	using StockSharp.SmartCom;
	using StockSharp.Algo;
	using StockSharp.Localization;

	class Program
	{
		private static Security _lkoh;
		private static Portfolio _portfolio;

		static void Main()
		{
			try
			{
				// для теста выбираем бумагу Лукойл
				const string secCode = "LKOH";

				Console.Write("{0}: ", LocalizedStrings.EnterLogin);
				var login = Console.ReadLine();

				Console.Write("{0}: ", LocalizedStrings.EnterPassword);
				var password = Console.ReadLine();

				Console.Write("Enter account number through which an order will be placed: ".Translate());
				var account = Console.ReadLine();

				using (var waitHandle = new AutoResetEvent(false))
				{
					// создаем подключение к Smart-у
					using (var trader = new SmartTrader { Login = login, Password = password, Address = SmartComAddresses.Reserve1 })
					{
                        trader.Version = StockSharp.SmartCom.Native.SmartComVersions.V2;
                        // подписываемся на событие успешного подключения
                        // все действия необходимо производить только после подключения
                        trader.Connected += () =>
						{
							Console.WriteLine(LocalizedStrings.Str2169);

							// извещаем об успешном соединени
							waitHandle.Set();
						};

						Console.WriteLine(LocalizedStrings.Str2170);

						trader.Connect();

						// дожидаемся события об успешном соединении
						waitHandle.WaitOne();

						// подписываемся на все портфели-счета
						trader.NewPortfolios += portfolios =>
						{
							if (_portfolio != null)
								return;

							// находим нужный портфель и присваиваем его переменной _portfolio
							_portfolio = portfolios.FirstOrDefault(p => p.Name == account);

							if (_portfolio == null)
								return;

							Console.WriteLine(LocalizedStrings.Str2171Params, account);

							if (_lkoh != null)
								waitHandle.Set();
						};

						// подписываемся на событие появление инструментов
						trader.NewSecurities += securities =>
						{
							if (_lkoh == null)
							{
								// находим Лукойл и присваиваем ее переменной lkoh
								_lkoh = securities.FirstOrDefault(sec => sec.Code == secCode && sec.Type == SecurityTypes.Stock);

								if (_lkoh != null)
								{
									Console.WriteLine(LocalizedStrings.Str2987);

									if (_portfolio != null)
										waitHandle.Set();
								}
							}
						};

						// подписываемся на событие появления моих новых сделок
						trader.NewMyTrades += myTrades =>
						{
							foreach (var myTrade in myTrades)
							{
								var trade = myTrade.Trade;
								Console.WriteLine(LocalizedStrings.Str2173Params, trade.Id, trade.Price, trade.Security.Code, trade.Volume, trade.Time);
							}
						};

						Console.WriteLine(LocalizedStrings.Str2989Params.Put(account));

						// дожидаемся появления портфеля и инструмента
						waitHandle.WaitOne();

						trader.SecuritiesChanged += securities =>
						{
							// если инструмент хоть раз изменился (по нему пришли актуальные данные)
							if (securities.Contains(_lkoh) && _lkoh.BestBid != null && _lkoh.BestAsk != null)
								waitHandle.Set();
						};

						Console.WriteLine("Waiting for Lukoil security data to update...".Translate());

						// запускаем обновление по инструменту
						trader.RegisterSecurity(_lkoh);
						waitHandle.WaitOne();

						// 0.1% от изменения цены
						const decimal delta = 0.001m;

						// запоминаем первоначальное значение середины спреда
						var firstMid = _lkoh.BestPair.SpreadPrice / 2;
						if (_lkoh.BestBid == null || firstMid == null)
							throw new Exception(LocalizedStrings.Str2990);

						Console.WriteLine(LocalizedStrings.Str2991Params, _lkoh.BestBid.Price + firstMid);

						while (true)
						{
							var mid = _lkoh.BestPair.SpreadPrice / 2;

							// если спред вышел за пределы нашего диапазона
							if (mid != null &&
								((firstMid + firstMid * delta) <= mid ||
								(firstMid - firstMid * delta) >= mid)
								)
							{
								var order = new Order
								{
									Portfolio = _portfolio,
									Price = _lkoh.ShrinkPrice(_lkoh.BestBid.Price + mid.Value),
									Security = _lkoh,
									Volume = 1,
									Direction = Sides.Buy,
								};
								//trader.RegisterOrder(order);
								Console.WriteLine(LocalizedStrings.Str1157Params, order.Id);
								break;
							}
							else
								Console.WriteLine(LocalizedStrings.Str2176Params, _lkoh.BestBid.Price + mid);

							// ждем 1 секунду
							Thread.Sleep(1000);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}
	}
}