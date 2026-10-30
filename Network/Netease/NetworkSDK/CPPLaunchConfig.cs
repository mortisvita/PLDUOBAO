using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests;

namespace PLDUOBAO.Network.Netease.NetworkSDK
{
    [JsonSerializable(typeof(CPPLaunchConfig))]
    internal partial class CPPLaunchConfigContext : JsonSerializerContext { }
    internal class CPPLaunchConfig
    {
        public WorldInfo world_info { get; set; } = new WorldInfo();
        public RoomInfo? room_info { get; set; } = new RoomInfo();
        public PlayerInfo? player_info { get; set; } = new PlayerInfo();
        public SkinInfo? skin_info { get; set; } = new SkinInfo();
        public AntiAddictionInfo? anti_addiction_info { get; set; } = new AntiAddictionInfo();
        public Misc? misc { get; set; } = new Misc();
        public string? path { get; set; }
        public string? web_server_url { get; set; }
        public string? core_server_url { get; set; }
        public List<string>? vip_using_mod { get; set; } = new List<string>();
        public ResultInfo? Result { get; set; } = new ResultInfo();

        public static CPPLaunchConfig GenerateNew(string ip,
                                                  int port, 
                                                  string skinPath,
                                                  bool isSlimSkin,
                                                  AuthcationResponseEntity user,
                                                  string userName, 
                                                  int localAuthPort)
        {
            string skinHash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(skinPath)));
            CPPLaunchConfig cfg = new CPPLaunchConfig()
            {
                world_info = new WorldInfo()
                {
                    level_id = "114514",
                    name = "PLDUOBAO Launcher",
                },
                room_info = new RoomInfo()
                {
                    ip = ip,
                    port = (int)port,
                    token = "PLDUOBAO",
                    item_ids = new()
                },
                player_info = new PlayerInfo()
                {
                    user_id = uint.Parse(user.entity_id!),
                    user_name = userName,
                    urs = user.entity_id,
                },
                skin_info = new SkinInfo()
                {
                    skin = skinPath.Replace("\\\\", "\\"),
                    md5 = skinHash.ToLower(),
                    slim = isSlimSkin,
                    skin_iid = "-123456"
                },
                misc = new Misc()
                {
                    multiplayer_game_type = 100,
                    multi_player_type = 100,
                    auth_server_url = $"http://127.0.0.1:{localAuthPort}",
                    launcher_port = 0
                },
                web_server_url = "https://x19apigatewayobt.nie.netease.com",
                core_server_url = "https://x19obtcore.nie.netease.com:8443",

            };
            return cfg;
        }

        public static CPPLaunchConfig GenerateLocal(string skinPath,
                                                    bool isSlimSkin,
                                                    AuthcationResponseEntity user,
                                                    string userName,
                                                    int localAuthPort,
                                                    string worldName = "PLDUOBAO Local World",
                                                    string worldId = "114514")
        {
            string skinHash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(skinPath)));
            CPPLaunchConfig cfg = new CPPLaunchConfig()
            {
                world_info = new WorldInfo()
                {
                    level_id = worldId,
                    name = worldName,
                },
                room_info = new RoomInfo()
                {
                    ip = "127.0.0.1",
                    port = 19132,
                    token = "PLDUOBAO",
                    item_ids = new()
                },
                player_info = new PlayerInfo()
                {
                    user_id = uint.Parse(user.entity_id!),
                    user_name = userName,
                    urs = user.entity_id,
                },
                skin_info = new SkinInfo()
                {
                    skin = skinPath.Replace("\\\\", "\\"),
                    md5 = skinHash.ToLower(),
                    slim = isSlimSkin,
                    skin_iid = "-123456"
                },
                misc = new Misc()
                {
                    multiplayer_game_type = 0, // 本地世界类型
                    multi_player_type = 0,
                    auth_server_url = $"http://127.0.0.1:{localAuthPort}",
                    launcher_port = 0
                },
                web_server_url = "https://x19apigatewayobt.nie.netease.com",
                core_server_url = "https://x19obtcore.nie.netease.com:8443",
            };
            return cfg;
        }

        public static CPPLaunchConfig GenerateNewWorld(string skinPath,
                                                       bool isSlimSkin,
                                                       AuthcationResponseEntity user,
                                                       string userName,
                                                       int localAuthPort,
                                                       WorldInfo worldInfo)
        {
            string skinHash = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(skinPath)));
            CPPLaunchConfig cfg = new CPPLaunchConfig()
            {
                world_info = worldInfo,
                room_info = new RoomInfo()
                {
                    ip = "127.0.0.1",
                    port = 19132,
                    token = "PLDUOBAO",
                    item_ids = new()
                },
                player_info = new PlayerInfo()
                {
                    user_id = uint.Parse(user.entity_id!),
                    user_name = userName,
                    urs = user.entity_id,
                },
                skin_info = new SkinInfo()
                {
                    skin = skinPath.Replace("\\\\", "\\"),
                    md5 = skinHash.ToLower(),
                    slim = isSlimSkin,
                    skin_iid = "-123456"
                },
                misc = new Misc()
                {
                    multiplayer_game_type = 0,
                    multi_player_type = 0,
                    auth_server_url = localAuthPort > 0 ? $"http://127.0.0.1:{localAuthPort}" : "http://127.0.0.1:19134",
                    launcher_port = 0
                },
                web_server_url = "https://x19apigatewayobt.nie.netease.com",
                core_server_url = "https://x19obtcore.nie.netease.com:8443",
            };
            return cfg;
        }
    }

    public class WorldInfo
    {
        public class CheatInfo
        {
            public bool pvp { get; set; } = true;

            public bool show_coordinates { get; set; }

            public bool always_day { get; set; }

            public bool daylight_cycle { get; set; } = true;

            public bool fire_spreads { get; set; } = true;

            public bool tnt_explodes { get; set; } = true;

            public bool keep_inventory { get; set; }

            public bool mob_spawn { get; set; } = true;

            public bool natural_regeneration { get; set; } = true;

            public bool mob_loot { get; set; } = true;

            public bool mob_griefing { get; set; } = true;

            public bool tile_drops { get; set; } = true;

            public bool entities_drop_loot { get; set; } = true;

            public bool weather_cycle { get; set; } = true;

            public bool command_blocks_enabled { get; set; } = true;

            public uint random_tick_speed { get; set; } = 1u;

            public bool experimental_gameplay { get; set; }

            public bool experimental_holiday { get; set; }

            public bool experimental_biomes { get; set; }

            public bool experimental_modding { get; set; }

            public bool fancy_bubbles { get; set; }
        }
        public string level_id { get; set; } = "";

        public int game_type { get; set; }

        public int difficulty { get; set; } = 2;

        public int permission_level { get; set; } = 1;

        public bool cheat { get; set; }

        public bool other_cheat { get; set; }

        public CheatInfo cheat_info { get; set; } = new CheatInfo();

        public List<string> resource_packs { get; set; } = new List<string>();

        public List<string> behavior_packs { get; set; } = new List<string>();

        public string name { get; set; } = "";

        public int world_type { get; set; } = 1;

        public int generator { get; set; } = 1;  // 0=旧版, 1=无限, 2=超平坦

        public bool start_with_map { get; set; }

        public bool bonus_items { get; set; }

        public string seed { get; set; } = "";
    }

    public class RoomInfo
    {
        public string ip { get; set; } = "";
        public int port { get; set; }

        public string room_name { get; set; } = "";

        public string enter_password { get; set; } = "";

        public string token { get; set; } = "";

        public uint room_id { get; set; }

        public uint host_id { get; set; }

        public bool allow_pe { get; set; } = true;

        public int max_player { get; set; }

        public int visibility_mode { get; set; }

        public List<string> item_ids { get; set; } = new List<string>();

        public List<uint>? tag_ids { get; set; }

        public int simple_game_version { get; set; }

        public string? create_room_extra_bits { get; set; }
    }

    public class PlayerInfo
    {
        public uint user_id { get; set; }
        public string? user_name { get; set; }
        public string? urs { get; set; }
    }

    public class SkinInfo
    {
        public string? skin { get; set; }
        public string? md5 { get; set; }
        public bool slim { get; set; }
        public string? skin_iid { get; set; }
    }

    public class Misc
    {
        public int multiplayer_game_type { get; set; }
        public int multi_player_type { get; set; }
        public string? auth_server_url { get; set; }
        public int launcher_port { get; set; } = 0;
        public string? sensitive_word_file { get; set; } = "";
    }

    public class AntiAddictionInfo
    {
        public bool enable { get; set; }
        public int left_time { get; set; }
        public double exp_multiplier { get; set; } = 1.0;
        public double block_multplier { get; set; } = 1.0;
        public string? first_message { get; set; } = "";
    }

    public class ResultInfo
    {
        public bool a { get; set; } = true;
        public string? b { get; set; } = "";
        public object? c { get; set; }
    }

}
