using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Uchu.Core
{
    public static class ServerHelper
    {
        public static async Task<ServerSpecification> RequestWorldServerAsync(ZoneId zoneId)
        {
            //
            // Check for servers
            //

            var worldServers = await GetServersByType(ServerType.World).ConfigureAwait(false);
            
            foreach (var worldServer in worldServers.Where(w => w.ZoneId == zoneId))
            {
                if (worldServer.ActiveUserCount >= worldServer.MaxUserCount) continue;

                return worldServer;
            }
            
            //
            // Start a new world server request.
            //
            
            var id = Guid.NewGuid();

            await using (var ctx = new UchuContext())
            {
                await ctx.WorldServerRequests.AddAsync(new WorldServerRequest
                {
                    Id = id,
                    ZoneId = zoneId
                }).ConfigureAwait(false);

                await ctx.SaveChangesAsync().ConfigureAwait(false);
            }

            //
            // We have to have a timeout here, if the new server instance stops unexpectedly of whatever.
            //
            
            var timeout = 1000;
            
            while (timeout != default)
            {
                //
                // Get request.
                //
                
                await using var ctx = new UchuContext();

                var request = await ctx.WorldServerRequests.FirstAsync(
                    r => r.Id == id
                ).ConfigureAwait(false);

                //
                // Check request state
                //
                
                switch (request.State)
                {
                    case WorldServerRequestState.Unanswered:
                    case WorldServerRequestState.Answered:
                        timeout--;
                            
                        await Task.Delay(100).ConfigureAwait(false);
                        
                        continue;
                    case WorldServerRequestState.Complete:
                        break;
                    case WorldServerRequestState.Error:
                        return default;
                    default:
                        return default;
                }
                
                Logger.Information($"Request completed {id} {request.SpecificationId}");
                
                //
                // Finalize request.
                //
                
                ctx.WorldServerRequests.Remove(request);

                await ctx.SaveChangesAsync().ConfigureAwait(false);
                    
                var specification = await ctx.Specifications.FirstAsync(s => s.Id == request.SpecificationId).ConfigureAwait(false);

                return specification;
            }
                
            Logger.Error($"Request {id} timed out");

            return default;
        }

        public static async Task<ServerSpecification> GetServerByType(ServerType type)
        {
            await using var ctx = new UchuContext();

            return await ctx.Specifications.FirstOrDefaultAsync(s => s.ServerType == type).ConfigureAwait(false);
        }
                
        public static async Task<ServerSpecification[]> GetServersByType(ServerType type)
        {
            await using var ctx = new UchuContext();

            return await ctx.Specifications.Where(s => s.ServerType == type).ToArrayAsync().ConfigureAwait(false);
        }
    }
}