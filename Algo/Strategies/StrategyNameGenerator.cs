namespace StockSharp.Algo.Strategies
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Ecng.Collections;
	using Ecng.Common;

	using SmartFormat;
	using SmartFormat.Core.Extensions;
	using SmartFormat.Core.Parsing;

	/// <summary>
	/// The class for the strategy name formation.
	/// </summary>
	public sealed class StrategyNameGenerator
	{
		private sealed class Source : ISource
		{
			private readonly Dictionary<string, string> _values;

			public Source(SmartFormatter formatter, Dictionary<string, string> values)
			{
				if (formatter == null)
					throw new ArgumentNullException("formatter");

				if (values == null)
					throw new ArgumentNullException("values");

				formatter.Parser.AddAlphanumericSelectors();
				formatter.Parser.AddAdditionalSelectorChars("_");
				formatter.Parser.AddOperators(".");

				_values = values;
			}

			bool ISource.TryEvaluateSelector(ISelectorInfo selectorInfo)
			{
				if (_values == null)
					return false;

				var value = _values.TryGetValue(selectorInfo.Selector.Text);

				if (value == null)
					return false;

				selectorInfo.Result = value;
				return true;
			}
		}

		private readonly SmartFormatter _formatter;
		private readonly Strategy _strategy;
		private readonly SynchronizedSet<string> _selectors;
		private string _value;
		private string _pattern;

		/// <summary>
		/// Initializes a new instance of the <see cref="StrategyNameGenerator"/>.
		/// </summary>
		/// <param name="strategy">Strategy.</param>
		public StrategyNameGenerator(Strategy strategy)
		{
			if (strategy == null)
				throw new ArgumentNullException("strategy");

			_strategy = strategy;
			_strategy.SecurityChanged += () =>
			{
				if (_selectors.Contains("Security"))
					Refresh();
			};
			_strategy.PortfolioChanged += () =>
			{
				if (_selectors.Contains("Portfolio"))
					Refresh();
			};

			ShortName = new string(_strategy.GetType().Name.Where(char.IsUpper).ToArray());

			_formatter = Smart.CreateDefaultSmartFormat();
			_formatter.SourceExtensions.Add(new Source(_formatter, new Dictionary<string, string>
			{
				{ "FullName", _strategy.GetType().Name },
				{ "ShortName", ShortName },
			}));

			_selectors = new SynchronizedSet<string>();

			AutoGenerateStrategyName = true;
			Pattern = "{ShortName}{Security:_{0.Security}|}{Portfolio:_{0.Portfolio}|}";
		}

		/// <summary>
		/// The name change event.
		/// </summary>
		public event Action<string> Changed;

		/// <summary>
		/// Whether to use the automatic generation of the strategy name. It is enabled by default.
		/// </summary>
		public bool AutoGenerateStrategyName { get; set; }

		/// <summary>
		/// The startegy brief name.
		/// </summary>
		public string ShortName { get; private set; }

		/// <summary>
		/// The pattern for strategy name formation.
		/// </summary>
		public string Pattern
		{
			get { return _pattern; }
			set
			{
				if (_pattern == value)
					return;

				_pattern = value;

				var format = _formatter.Parser.ParseFormat(value);
				var selectors = format
					.Items
					.OfType<Placeholder>()
					.SelectMany(ph => ph.Selectors)
					.Select(s => s.Text)
					.Distinct();

				_selectors.Clear();
				_selectors.AddRange(selectors);

				Refresh();
			}
		}

		/// <summary>
		/// Generated or set strategy name.
		/// </summary>
		public string Value
		{
			get { return _value ?? (_value = _strategy.Name); }
			set
			{
				if (AutoGenerateStrategyName)
					AutoGenerateStrategyName = false;
					//throw new InvalidOperationException("Используется автоматическая генерация имени стратегии. Ручное изменение не допускается.");

				_value = value;
				Changed.SafeInvoke(_value);
			}
		}

		private void Refresh()
		{
			if (!AutoGenerateStrategyName)
				return;

			_value = _formatter.Format(Pattern, _strategy);
			Changed.SafeInvoke(_value);
		}
	}
}