using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Caching;
using System.Collections.Specialized;
using Priority_Queue;

namespace MR
{
    class MiniRedis
    {
        // The 2 stores - we can ideally do with only one of them
        // kvstore allows expiry and TTL for objects pqueue does not 
        // kv store is a key value store, pqueue is a dictionary<key(string),pqueue(PriorityQueue)>
        private Cache kvStore;
        private Pqueue pqueue;

        //makes this single instance = find a way to generate a new random string for multiple instances
        //so each instance would have a different cache
        private static String mcName = "MiniRedis";

        // a global lock for every operation - very inefficient
        // concurrent dictionary is thread safe for upserts, but didn't natively implement memory limit/TTL
        // memory cache is thread safe in general, but not for upserts, which requires incrementing.
        // the SimplePriorityQueue operations is thread safe too 
        // (https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp/wiki/Using-the-SimplePriorityQueue)
        private Object mrLock = new Object();
        public MiniRedis()
        {
            //kvStore = new KVStore();
            kvStore = new Cache(mcName);
            pqueue = new Pqueue();
        }

        public String Handle(String request)
        {
            String stdReturn = "OK";
            String stdNil = "(nil)";
            String stdError = "Invalid Command";
            String[] reqParts = request.Split(' ');
            // This lock is because we want incr to be thread safe. This is very inefficient.
            // Make sure we push the lock down to the execution part for faster isolation levels.
            lock (mrLock)
            #region global lock if required
            {
                switch (reqParts[0].ToUpper())
                {
                    case "SET":
                        if (reqParts.Length == 3)
                        {
                            kvStore.set(reqParts[1], reqParts[2]);
                            //System.Threading.Thread.Sleep(new TimeSpan(0, 0, 10));
                            return stdReturn;
                        }
                        else if (reqParts.Length == 4)
                        {
                            kvStore.set(reqParts[1], reqParts[2], reqParts[3]);
                            return stdReturn;
                        }
                        else
                        {
                            return stdError;
                        }
                    case "GET":
                        if (reqParts.Length == 2)
                        {
                            String retVal = kvStore.get(reqParts[1]);
                            return string.IsNullOrEmpty(retVal) ? stdNil : retVal;
                        }
                        else
                        {
                            return stdError;
                        }
                    case "DEL":
                        for(int i = 1; i < reqParts.Length; i++)
                        {
                            kvStore.delete(reqParts[i]);
                        }
                        return stdReturn;
                    case "DBSIZE":
                        return kvStore.size();
                    case "INCR":
                        return kvStore.increment(reqParts[1]);
                    case "ZADD":
                        if (reqParts.Length == 4)
                        {
                            pqueue.add(reqParts[1], reqParts[2], reqParts[3]);
                            return stdReturn;
                        }
                        else
                        {
                            return stdError;
                        }
                    case "ZCARD":
                        return pqueue.countAtKey(reqParts[1]);
                    default:
                        return stdError;
                }
            }
            #endregion
        }

        class Pqueue
        {
            private Dictionary<String, SimplePriorityQueue<String>> model;
            
            public Pqueue()
            {
                model = new Dictionary<string, SimplePriorityQueue<string>>();
            }

            public void add(String key, String value, String priority)
            {
                int pri;
                if(int.TryParse(priority, out pri))
                {
                    //is there a better way to represent this logic than to have 2 separate enqueues?
                    if (!model.ContainsKey(key))
                    {
                        model[key] = new SimplePriorityQueue<string>();
                        model[key].Enqueue(value, pri);
                    }
                    else if (model[key].Contains(value))
                    {
                        model[key].UpdatePriority(value, pri);
                    }
                    else
                    {
                        model[key].Enqueue(value, pri);
                    }
                }
            }

            public string countAtKey(String key)
            {
                if (model.ContainsKey(key))
                {
                    return model[key].Count().ToString();
                }
                else
                {
                    return null;
                }
            }
        }

        class Cache
        {
            MemoryCache model;
            public Cache(String name)
            {
                var config = new NameValueCollection();
                // Limit it to 10 MB, LRU eviction. May be have this as a static variable?
                config.Add("cacheMemoryLimitMegabytes", "10");
                model = new MemoryCache(name, config);
            }

            public void set(String key, String value)
            {
                model[key] = value;
            }

            public void set(String key, String value, String ttlSeconds)
            {
                int seconds;
                if(Int32.TryParse(ttlSeconds, out seconds))
                {
                    var policy = new CacheItemPolicy
                    {
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(seconds)
                    };
                    model.Set(key, value, policy);
                }
            }

            public String get(String key)
            {
                return (string)model[key];
            }

            public void delete(String key)
            {
                model.Remove(key);
            }

            public String size()
            {
                return model.GetCount().ToString();   
            }

            public string increment(String key)
            {
                if (!model.Contains(key))
                    model[key] = "0";
                int val;
                if (Int32.TryParse((string)model[key], out val))
                {
                    val += 1;
                    model[key] = val.ToString();
                    return val.ToString();
                }
                else
                {
                    return null;
                }
            }
        }

        // Currently not used, replaced by MemoryCache which serves the purpose better
        class KVStore
        {
            ConcurrentDictionary<String, String> model;
            public KVStore()
            {
                model = new ConcurrentDictionary<string, string>();
            }

            public void set(String key, String value)
            {
                model[key] = value;
            }

            public String get(String key)
            {
                String value = null;
                model.TryGetValue(key, out value);
                return value;
            }

            public void delete(String key)
            {
                String ret;
                model.TryRemove(key, out ret);
            }

            public String size()
            {
                return model.Count().ToString();
            }

            public string increment(String key)
            {
                try
                {
                    return model.AddOrUpdate(key, "1", (id, count) => (int.Parse(count) + 1).ToString());
                }
                catch(FormatException fe)
                {
                    return null;
                }
            }
        }
    }
}
