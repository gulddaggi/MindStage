using System;
using System.Collections.Generic;
using App.Auth;
using App.Services;
using Unity.VisualScripting;


namespace App.Infra
{
    /// <summary>MVP 단계용 서비스 허브. 전역에서 공용 서비스 인스턴스를 등록/해제/해결한다.</summary>

    public static class Services
    {
        // 내부 컨테이너 (타입 → 인스턴스)
        static readonly object _gate = new object();
        static readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        /// <summary>서비스 등록. 기본은 덮어쓰기 허용.</summary>
        public static void Register<T>(T instance, bool overwrite = true) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (_gate)
            {
                var key = typeof(T);
                if (_map.ContainsKey(key))
                {
                    if (!overwrite)
                        throw new InvalidOperationException($"Service already registered: {key.Name}");
                    _map[key] = instance;
                }
                else
                {
                    _map.Add(key, instance);
                }
            }
        }

        /// <summary>서비스 조회(없으면 예외). 필수 서비스에 사용.</summary>
        public static T Resolve<T>() where T : class
        {
            lock (_gate)
            {
                if (_map.TryGetValue(typeof(T), out var obj) && obj is T t)
                    return t;
            }
            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }

        /// <summary>서비스 조회(없으면 null). 선택 서비스에 사용.</summary>
        public static T TryResolve<T>() where T : class
        {
            lock (_gate)
            {
                if (_map.TryGetValue(typeof(T), out var obj) && obj is T t)
                    return t;
            }
            return null;
        }

        /// <summary>등록 여부.</summary>
        public static bool IsRegistered<T>() where T : class
        {
            lock (_gate) return _map.ContainsKey(typeof(T));
        }

        /// <summary>서비스 해제.</summary>
        public static void Unregister<T>() where T : class
        {
            lock (_gate) _map.Remove(typeof(T));
        }

        /// <summary>모든 서비스 초기화.</summary>
        public static void Clear()
        {
            lock (_gate) _map.Clear();
        }

        // ─────────────────────────────────────────────────────────────
        // 기존 프로퍼티 호환 계층(내부적으로 Register/Resolve를 사용)
        // ─────────────────────────────────────────────────────────────

        public static IAuthService Auth
        {
            get => TryResolve<IAuthService>();
            set => Register<IAuthService>(value, overwrite: true);
        }

        // ILookupService / IResumeService가 전역(혹은 동일 네임스페이스)이라 using 불필요
        public static ILookupService Lookup
        {
            get => TryResolve<ILookupService>();
            set => Register<ILookupService>(value, overwrite: true);
        }

        public static IResumeService Resume
        {
            get => TryResolve<IResumeService>();
            set => Register<IResumeService>(value, overwrite: true);
        }

        public static ITtsProvider Tts
        {
            get => TryResolve<ITtsProvider>();
            set => Register<ITtsProvider>(value, overwrite: true);
        }

        public static ISttService Stt
        {
            get => TryResolve<ISttService>();
            set => Register<ISttService>(value, overwrite: true);
        }

        public static IReportService Report
        {
            get => TryResolve<IReportService>();
            set => Register<IReportService>(value, overwrite: true);
        }

        public static IUserService User
        {
            get => TryResolve<IUserService>();
            set => Register<IUserService>(value, overwrite: true);
        }

        public static IWearLinkService WearLink
        {
            get => TryResolve<IWearLinkService>();
            set => Register<IWearLinkService>(value, overwrite: true);
        }
    }
}
