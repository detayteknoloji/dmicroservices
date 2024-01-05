using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StackExchange.Redis;

namespace DMicroservices.DataAccess.Redis
{

    public class RedisList<T> : IList<T>
    {
        private static readonly string Domain = Environment.GetEnvironmentVariable("REDIS_URL");
        private ConnectionMultiplexer Connection { get; set; }
        private IDatabase RedisDatabase { get; set; }

        private string _key;
        public RedisList(string key)
        {
            _key = key;

            ConfigurationOptions options = ConfigurationOptions.Parse(Domain);
            Connection = ConnectionMultiplexer.Connect(options);
            RedisDatabase = Connection.GetDatabase();
        }
        private IDatabase GetRedisDb()
        {
            return Connection.GetDatabase();
        }
        private byte[] Serialize(object obj)
        {
            return MessagePack.MessagePackSerializer.Serialize(obj, MessagePack.MessagePackSerializer.DefaultOptions.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance));
        }
        private T Deserialize<T>(byte[] serialized)
        {
            return MessagePack.MessagePackSerializer.Deserialize<T>(serialized, MessagePack.MessagePackSerializer.DefaultOptions.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance));
        }
        public void Insert(int index, T item)
        {
            var db = GetRedisDb();
            var before = db.ListGetByIndex(_key, index);
            db.ListInsertBefore(_key, before, Serialize(item));
        }
        public void RemoveAt(int index)
        {
            var db = GetRedisDb();
            var value = db.ListGetByIndex(_key, index);
            if (!value.IsNull)
            {
                db.ListRemove(_key, value);
            }
        }
        public T this[int index]
        {
            get
            {
                var value = GetRedisDb().ListGetByIndex(_key, index);
                return Deserialize<T>(value);
            }
            set
            {
                Insert(index, value);
            }
        }
        public void Add(T item)
        {
            GetRedisDb().ListRightPush(_key, Serialize(item));
        }
        public void Add(T item, TimeSpan expireTime)
        {
            GetRedisDb().ListRightPush(_key, Serialize(item));

            GetRedisDb().KeyExpire(_key, expireTime);
        }

        public void AddRange(IEnumerable<T> collection, TimeSpan? expireTime = null)
        {
            GetRedisDb().ListRightPush(_key, collection.Select(x => (RedisValue)Serialize(x)).ToArray());

            if (expireTime == null)
                expireTime = new TimeSpan(0, 10, 0);

            GetRedisDb().KeyExpire(_key, expireTime);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            GetRedisDb().ListRightPush(_key, collection.Select(x => (RedisValue)Serialize(x)).ToArray());
        }

        public void Clear()
        {
            GetRedisDb().KeyDelete(_key);
        }
        public bool Contains(T item)
        {
            for (int i = 0; i < Count; i++)
            {
                if (GetRedisDb().ListGetByIndex(_key, i).ToString().Equals(Serialize(item)))
                {
                    return true;
                }
            }
            return false;
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            var range = GetRedisDb().ListRange(_key);
            for (var i = 0; i < range.Length; i++)
            {
                array[i] = Deserialize<T>(range[i]);
            }
        }
        public int IndexOf(T item)
        {
            for (int i = 0; i < Count; i++)
            {
                if (GetRedisDb().ListGetByIndex(_key, i).ToString().Equals(Serialize(item)))
                {
                    return i;
                }
            }
            return -1;
        }
        public int Count
        {
            get { return (int)GetRedisDb().ListLength(_key); }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        public bool Remove(T item)
        {
            return GetRedisDb().ListRemove(_key, Serialize(item)) > 0;
        }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.Count; i++)
            {
                yield return Deserialize<T>(GetRedisDb().ListGetByIndex(_key, i));
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.Count; i++)
            {
                yield return Deserialize<T>(GetRedisDb().ListGetByIndex(_key, i));
            }
        }
    }
}
