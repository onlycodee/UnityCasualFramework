using System;
using System.Collections.Generic;
using HyperFrame.Core;

namespace HyperFrame.Services
{
    public static class Currencies
    {
        /// <summary>Soft currency earned by playing.</summary>
        public const string Coins = "coins";
        /// <summary>Hard currency (IAP).</summary>
        public const string Gems = "gems";
    }

    /// <summary>Published on the event bus after every balance change (also feeds analytics currency_change).</summary>
    public struct CurrencyChangedEvent
    {
        public string Currency;
        public long Delta;
        public long Balance;
        public string Source;
    }

    public interface IWallet
    {
        long Get(string currency);
        bool CanAfford(string currency, long amount);
        void Add(string currency, long amount, string source);
        bool TrySpend(string currency, long amount, string source);
        event Action<CurrencyChangedEvent> Changed;
    }

    /// <summary>Currencies and balances (EC-01), persisted in the "wallet" save section.</summary>
    public sealed class Wallet : IWallet
    {
        public const string Section = "wallet";

        [Serializable]
        sealed class Data { public Dictionary<string, long> balances = new Dictionary<string, long>(); }

        readonly ISaveService _save;
        readonly IEventBus _events;
        readonly Data _data;

        public event Action<CurrencyChangedEvent> Changed;

        public Wallet(ISaveService save, IEventBus events = null, IDictionary<string, long> startingBalances = null)
        {
            _save = save;
            _events = events;
            bool fresh = !save.Has(Section);
            _data = save.Get<Data>(Section);
            if (_data.balances == null) _data.balances = new Dictionary<string, long>();
            if (fresh && startingBalances != null)
            {
                foreach (var kv in startingBalances) _data.balances[kv.Key] = kv.Value;
                Persist();
            }
        }

        public long Get(string currency) => _data.balances.TryGetValue(currency, out var v) ? v : 0;

        public bool CanAfford(string currency, long amount) => amount >= 0 && Get(currency) >= amount;

        public void Add(string currency, long amount, string source)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove currency.");
            if (amount == 0) return;
            Apply(currency, amount, source);
        }

        public bool TrySpend(string currency, long amount, string source)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAfford(currency, amount)) return false;
            if (amount > 0) Apply(currency, -amount, source);
            return true;
        }

        void Apply(string currency, long delta, string source)
        {
            long balance = Get(currency) + delta;
            _data.balances[currency] = balance;
            Persist();
            var evt = new CurrencyChangedEvent { Currency = currency, Delta = delta, Balance = balance, Source = source ?? "unknown" };
            Changed?.Invoke(evt);
            _events?.Publish(evt);
        }

        void Persist() => _save.Set(Section, _data);
    }
}
