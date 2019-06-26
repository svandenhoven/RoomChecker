using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RoomChecker.Models;

namespace RoomChecker.Helpers
{
    public interface IEmbedService
    {
        EmbedConfig EmbedConfig { get; }
        TileEmbedConfig TileEmbedConfig { get; }

        Task<bool> EmbedReport(string userName, string roles);
        Task<bool> EmbedDashboard();
        Task<bool> EmbedTile();
    }
}
