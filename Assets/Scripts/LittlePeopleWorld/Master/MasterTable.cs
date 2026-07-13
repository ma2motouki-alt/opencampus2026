using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class MasterTable<TMaster> where TMaster : class
    {
        readonly Dictionary<int, TMaster> records = new();

        public MasterTable(IEnumerable<TMaster> source, Func<TMaster, int> keySelector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            foreach (var record in source)
            {
                records.Add(keySelector(record), record);
            }
        }

        public TMaster Get(int id)
        {
            if (records.TryGetValue(id, out var record))
            {
                return record;
            }

            throw new KeyNotFoundException($"Master record not found. type={typeof(TMaster).Name}, id={id}");
        }

        public bool TryGet(int id, out TMaster record)
        {
            return records.TryGetValue(id, out record);
        }
    }
}
