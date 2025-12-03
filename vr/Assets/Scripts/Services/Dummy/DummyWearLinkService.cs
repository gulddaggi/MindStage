using System.Threading.Tasks;
using UnityEngine;
using App.Core;

namespace App.Services
{
    public class DummyWearLinkService : IWearLinkService
    {
        const string KeyState = "wear.state";
        const string KeyUuid = "wear.uuid";
        const string KeyModel = "wear.modelName";
        const string KeyId = "wear.id";

        WearLinkStatus Read()
        {
            var st = new WearLinkStatus();
            st.state = (WearLinkState)PlayerPrefs.GetInt(KeyState, (int)WearLinkState.Disconnected);
            st.uuid = PlayerPrefs.GetString(KeyUuid, "");
            st.modelName = PlayerPrefs.GetString(KeyModel, "");
            st.galaxyWatchId = PlayerPrefs.HasKey(KeyId) ? PlayerPrefs.GetInt(KeyId) : (int?)null;
            st.ttlSeconds = 0;
            return st;
        }

        void Write(WearLinkStatus st)
        {
            PlayerPrefs.SetInt(KeyState, (int)st.state);
            PlayerPrefs.SetString(KeyUuid, st.uuid ?? "");
            PlayerPrefs.SetString(KeyModel, st.modelName ?? "");
            if (st.galaxyWatchId.HasValue) PlayerPrefs.SetInt(KeyId, st.galaxyWatchId.Value);
            else if (PlayerPrefs.HasKey(KeyId)) PlayerPrefs.DeleteKey(KeyId);
            PlayerPrefs.Save();
        }

        public Task<WearLinkStatus> GetStatusAsync() => Task.FromResult(Read());

        public Task<WearLinkStatus> RegisterAsync(WearLinkRegisterRequest req)
        {
            // 더미 정책: 값이 들어오면 곧장 Linked 처리
            var st = Read();
            if (!string.IsNullOrEmpty(req.uuid) || !string.IsNullOrEmpty(req.modelName))
            {
                st.uuid = req.uuid;
                st.modelName = req.modelName;
                st.galaxyWatchId = 1; // 임의의 값
                st.state = WearLinkState.Linked;
                Write(st);
            }
            return Task.FromResult(Read());
        }

        public Task UnlinkAsync()
        {
            var st = new WearLinkStatus { state = WearLinkState.Disconnected };
            Write(st);
            return Task.CompletedTask;
        }
    }
}
