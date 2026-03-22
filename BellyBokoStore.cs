using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGamePregnancyPlusBridge
{
    [DataContract]
    internal sealed class BellyBokoProfile
    {
        [DataMember(Order = 1)]
        public string AnimationKey = string.Empty;
        [DataMember(Order = 2)]
        public int PostureId = int.MinValue;
        [DataMember(Order = 3)]
        public int PostureMode = int.MinValue;
        [DataMember(Order = 4)]
        public string PostureName = string.Empty;
        [DataMember(Order = 5)]
        public string MotionStrength = "unknown";
        [DataMember(Order = 6)]
        public int AnimatorStateHash = 0;
        [DataMember(Order = 7)]
        public bool Enabled = true;
        [DataMember(Order = 8)]
        public int PresetSlot = 1;
        [DataMember(Order = 9)]
        public float ForwardMinPhase = 0.15f;
        [DataMember(Order = 10)]
        public float MinHoldWidth = 0f;
        [DataMember(Order = 11)]
        public float MaxPhase = 0.35f;
        [DataMember(Order = 12)]
        public float ReturnMinPhase = 0.55f;
        [DataMember(Order = 13)]
        public float MinInflationSize = 0f;
        [DataMember(Order = 14)]
        public float MaxInflationSize = 1f;
        [DataMember(Order = 15)]
        public string EaseUp = "easeOut";
        [DataMember(Order = 16)]
        public string EaseDown = "easeIn";
        [DataMember(Order = 17)]
        public bool DistanceMode = false;
        [DataMember(Order = 18)]
        public float DistanceCutPercent = 0.5f;
        [DataMember(Order = 19)]
        public float DistanceMinMeters = 0.04f;
        [DataMember(Order = 20)]
        public float DistanceMaxMeters = 0.24f;
        [DataMember(Order = 21)]
        public float DistanceSmoothing = 0.35f;
    }

    [DataContract]
    internal sealed class BellyBokoProfileCollection
    {
        [DataMember(Order = 1)]
        public List<BellyBokoProfile> Profiles = new List<BellyBokoProfile>();
    }

    internal sealed class BellyBokoStore
    {
        private readonly string _path;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarn;
        private BellyBokoProfileCollection _collection;

        public BellyBokoStore(string path, Action<string> logInfo, Action<string> logWarn)
        {
            _path = path;
            _logInfo = logInfo;
            _logWarn = logWarn;
            _collection = LoadOrCreate();
        }

        public bool TryGet(string animationKey, out BellyBokoProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(animationKey) || _collection?.Profiles == null)
                return false;

            BellyBokoProfile found = _collection.Profiles.FirstOrDefault(x =>
                x != null && string.Equals(x.AnimationKey ?? string.Empty, animationKey, StringComparison.Ordinal));
            if (found == null)
                return false;

            profile = Clone(found);
            return true;
        }

        public bool HasAnyForPostureStrength(int postureId, int postureMode, string motionStrength)
        {
            if (_collection?.Profiles == null)
                return false;

            string strength = motionStrength ?? string.Empty;
            for (int i = 0; i < _collection.Profiles.Count; i++)
            {
                BellyBokoProfile p = _collection.Profiles[i];
                if (p == null)
                    continue;
                if (p.PostureId != postureId)
                    continue;
                if (p.PostureMode != postureMode)
                    continue;
                if (!string.Equals(p.MotionStrength ?? string.Empty, strength, StringComparison.Ordinal))
                    continue;
                return true;
            }

            return false;
        }

        public void Upsert(BellyBokoProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.AnimationKey))
                return;

            EnsureCollection();

            for (int i = _collection.Profiles.Count - 1; i >= 0; i--)
            {
                BellyBokoProfile cur = _collection.Profiles[i];
                if (cur != null && string.Equals(cur.AnimationKey ?? string.Empty, profile.AnimationKey, StringComparison.Ordinal))
                    _collection.Profiles.RemoveAt(i);
            }

            _collection.Profiles.Add(Clone(profile));
            _collection.Profiles = _collection.Profiles
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AnimationKey))
                .OrderBy(x => x.AnimationKey, StringComparer.Ordinal)
                .ToList();
        }

        public void Save()
        {
            try
            {
                EnsureCollection();
                int countBefore = _collection.Profiles != null ? _collection.Profiles.Count : 0;
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = Serialize(_collection);
                string jsonHead = json == null ? "null" : (json.Length > 80 ? json.Substring(0, 80) : json);
                _logInfo?.Invoke("belly save diag: countBefore=" + countBefore + " jsonLen=" + (json == null ? -1 : json.Length) + " jsonHead=" + (jsonHead ?? string.Empty));
                File.WriteAllText(_path, json, new UTF8Encoding(false));

                long fileSize = -1;
                try { fileSize = new FileInfo(_path).Length; } catch { }

                int countReload = -1;
                try
                {
                    string re = File.ReadAllText(_path, Encoding.UTF8);
                    BellyBokoProfileCollection loaded = Deserialize(re);
                    countReload = loaded?.Profiles != null ? loaded.Profiles.Count : 0;
                }
                catch
                {
                    countReload = -2;
                }

                _logInfo?.Invoke("belly save diag: fileSize=" + fileSize + " reloadCount=" + countReload + " path=" + _path);
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("belly profile save failed: " + ex.Message);
            }
        }

        private BellyBokoProfileCollection LoadOrCreate()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    var empty = new BellyBokoProfileCollection();
                    string dir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(_path, Serialize(empty), new UTF8Encoding(false));
                    return empty;
                }

                string json = File.ReadAllText(_path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return new BellyBokoProfileCollection();

                BellyBokoProfileCollection loaded = Deserialize(json);
                if (loaded == null || loaded.Profiles == null)
                    return new BellyBokoProfileCollection();

                loaded.Profiles = loaded.Profiles
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AnimationKey))
                    .OrderBy(x => x.AnimationKey, StringComparer.Ordinal)
                    .ToList();
                return loaded;
            }
            catch (Exception ex)
            {
                _logWarn?.Invoke("belly profile load failed: " + ex.Message);
                return new BellyBokoProfileCollection();
            }
        }

        private void EnsureCollection()
        {
            if (_collection == null)
                _collection = new BellyBokoProfileCollection();
            if (_collection.Profiles == null)
                _collection.Profiles = new List<BellyBokoProfile>();
        }

        private static BellyBokoProfile Clone(BellyBokoProfile src)
        {
            return new BellyBokoProfile
            {
                AnimationKey = src.AnimationKey,
                PostureId = src.PostureId,
                PostureMode = src.PostureMode,
                PostureName = src.PostureName,
                MotionStrength = src.MotionStrength,
                AnimatorStateHash = src.AnimatorStateHash,
                Enabled = src.Enabled,
                PresetSlot = src.PresetSlot,
                ForwardMinPhase = src.ForwardMinPhase,
                MinHoldWidth = src.MinHoldWidth,
                MaxPhase = src.MaxPhase,
                ReturnMinPhase = src.ReturnMinPhase,
                MinInflationSize = src.MinInflationSize,
                MaxInflationSize = src.MaxInflationSize,
                EaseUp = src.EaseUp,
                EaseDown = src.EaseDown,
                DistanceMode = src.DistanceMode,
                DistanceCutPercent = src.DistanceCutPercent,
                DistanceMinMeters = src.DistanceMinMeters,
                DistanceMaxMeters = src.DistanceMaxMeters,
                DistanceSmoothing = src.DistanceSmoothing
            };
        }

        private static string Serialize(BellyBokoProfileCollection value)
        {
            if (value == null)
                value = new BellyBokoProfileCollection();

            var serializer = new DataContractJsonSerializer(typeof(BellyBokoProfileCollection));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static BellyBokoProfileCollection Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new BellyBokoProfileCollection();

            var serializer = new DataContractJsonSerializer(typeof(BellyBokoProfileCollection));
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                object obj = serializer.ReadObject(ms);
                return obj as BellyBokoProfileCollection ?? new BellyBokoProfileCollection();
            }
        }
    }
}
