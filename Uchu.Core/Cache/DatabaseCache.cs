using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace Uchu.Core
{
    public sealed class DatabaseCache : ISessionCache, IDisposable
    {
        private readonly RNGCryptoServiceProvider _rng;
        
        private readonly Dictionary<string, string> _keys = new Dictionary<string, string>();

        public DatabaseCache()
        {
            _rng = new RNGCryptoServiceProvider();
        }
        
        public string CreateSession(long userId)
        {
            using var ctx = new UchuContext();

            var key = GenerateKey();
            
            ctx.SessionCaches.Add(new SessionCache
            {
                Key = key,
                UserId = userId
            });

            ctx.SaveChanges();

            return key;
        }

        public void SetCharacter(IPEndPoint endpoint, long characterId)
        {
            using var ctx = new UchuContext();

            var key = _keys[endpoint.ToString()];

            var session = ctx.SessionCaches.First(s => s.Key == key);

            session.CharacterId = characterId;

            ctx.SaveChanges();
        }

        public void SetZone(IPEndPoint endpoint, ZoneId zone)
        {
            using var ctx = new UchuContext();

            var key = _keys[endpoint.ToString()];

            var session = ctx.SessionCaches.First(s => s.Key == key);

            session.ZoneId = (int) zone;

            ctx.SaveChanges();
        }

        public Session GetSession(IPEndPoint endpoint)
        {
            using var ctx = new UchuContext();

            var key = _keys[endpoint.ToString()];

            var session = ctx.SessionCaches.First(s => s.Key == key);
            
            return new Session
            {
                Key = key,
                CharacterId = session.CharacterId,
                UserId = session.UserId,
                ZoneId = session.ZoneId
            };
        }

        public bool IsKey(string key)
        {
            using var ctx = new UchuContext();

            return ctx.SessionCaches.Any(c => c.Key == key);
        }

        public void RegisterKey(IPEndPoint endPoint, string key)
        {
            _keys.Add(endPoint.ToString(), key);
        }

        public void DeleteSession(IPEndPoint endpoint)
        {
            using var ctx = new UchuContext();

            var key = _keys[endpoint.ToString()];

            var session = ctx.SessionCaches.First(s => s.Key == key);

            ctx.SessionCaches.Remove(session);

            ctx.SaveChanges();
        }
        
        private string GenerateKey(int length = 24)
        {
            var bytes = new byte[length];

            _rng.GetBytes(bytes);

            return Convert.ToBase64String(bytes).TrimEnd('=');
        }

        public void Dispose()
        {
            _rng.Dispose();
        }
    }
}