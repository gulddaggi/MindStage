using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using App.Infra;
using App.Auth;
using App.Services;

/// <summary>앱 기동 시 1회 실행되는 엔트리 포인트. 프레임/서비스 초기화 후 Title 씬으로 진입.</summary>

public class Bootstrap : MonoBehaviour
{
    [SerializeField] bool useDummyBackend = true; // 에디터 테스트 시 true

    async void Awake()
    {
        Application.targetFrameRate = 90;
        QualitySettings.vSyncCount = 0;

        HttpClientBase.BaseUrl = "https://mindstage.duckdns.org";

        // 서비스 바인딩
        if (useDummyBackend)
        {
            Services.Register<IAuthService>(new DummyAuthService());
            Services.Register<ILookupService>(new DummyLookupService());
            Services.Register<IResumeService>(new DummyResumeService());
            Services.Register<ITtsProvider>(new DummyTtsProvider());
            Services.Register<ISttService>(new DummySttService());
            Services.Register<IWearLinkService>(new DummyWearLinkService());
            Services.Register<IQuestionSetService>(new DummyQuestionSetService());
            //Services.Register<IS3Service>(new S3ApiService());
            Services.Register<IReportService>(new DummyReportService());
        }
        else
        {
            // 실 서비스 등록 (현재는 Auth만 실호출, 나머지는 추후 교체)
            Services.Register<IAuthService>(new AuthHttpService(HttpClientBase.BaseUrl));
            Services.Register<IUserService>(new UserHttpService(HttpClientBase.BaseUrl));
            Services.Register<IWearLinkService>(new WearLinkHttpService(HttpClientBase.BaseUrl));
            Services.Register<ILookupService>(new LookupApiService());
            Services.Register<IResumeService>(new ResumeApiService());
            Services.Register<IReportService>(new ReportApiService());

            // 나머지는 당분간 더미 유지 (추가 연동 시 실 구현으로 교체 예정)
            Services.Register<ITtsProvider>(new DummyTtsProvider());
            Services.Register<ISttService>(new DummySttService());
            Services.Register<IQuestionSetService>(new DummyQuestionSetService());
        }



        // 첫 화면 이동
        await SceneLoader.LoadSingleAsync(App.Infra.SceneIds.Title);
    }
}
