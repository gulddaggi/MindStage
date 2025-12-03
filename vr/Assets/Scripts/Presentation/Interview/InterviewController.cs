using App.Core;
using App.Infra;
using App.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using ServiceHub = App.Infra.Services;


namespace App.Presentation.Interview
{
    /// <summary>질문→준비→녹음→분석 루프를 오케스트레이션하는 면접 메인 컨트롤러.</summary>

    public class InterviewController : MonoBehaviour
    {
        [Header("Refs")]
        public MicRecorder mic;
        public AudioSource voiceSource;
        public Slider progressBar;      // 답변 진행바
        public TMP_Text hudText;        // 질문/상태 안내
        public TMP_Text bannerText;     // 배너(마이크 미설정/HMD 안내 등)
        public GameObject vrCanvas;     // World Space
        public GameObject desktopCanvas;// Screen Space
        public Button btnStop;

        public Button btnBack;
        public LipSyncRouter lipSyncRouter;

        [Header("Demo UI (1분 자기소개)")]
        public GameObject demoGuidePanel;
        public TMP_Text txtDemoScript;
        public Button btnGuide;       
        public Button btnGuideClose;

        [Header("Config")]
        [Min(0f)] public float askHoldSeconds = 2f;
        public float prepSeconds = 3f;
        public float answerSeconds = 60f;

        InterviewState _state;
        InterviewSession _session;
        InterviewSet _set;
        ITtsProvider _tts;
        ISttService _stt;
        readonly List<(int qid, string url, AudioClip clip, string difficult, int speakerLabel)> _qclips = new();
        int _lastSpeakerLabel = 0;
        //IS3Service _s3;
        readonly List<string> _uploadedFileKeys = new();
        readonly List<(string fileKey, string fileName)> _uploaded = new();


        public enum UIMode { Auto, Desktop, VR }

        [Header("UI Mode")]
        [SerializeField] UIMode startMode = UIMode.Desktop;   // ← 기본값을 Desktop으로
        [SerializeField] EventSystem eventSystem;             // EventSystem 오브젝트
        [SerializeField] InputSystemUIInputModule desktopUIModule; // 데스크톱용
        [SerializeField] XRUIInputModule xrUIModule;               // VR용

        [Header("Recording")]
        [SerializeField] bool saveLocalRecordings = true;
        [SerializeField] string recordingsFolder = "InterviewRecordings";
        [SerializeField, Range(0f, 5f)] float recordingTailSeconds = 3f;

        [Header("Intro Panel")]
        public GameObject introPanel;          // 면접 진행 안내 패널 루트
        public Button btnIntroStart;           // "면접 시작" 버튼
        public TMP_Text introTitleText;        // 안내 제목 텍스트
        public TMP_Text introBodyText;         // 안내 본문 텍스트

        public PanelModalError fatalModal;

        bool _stopRequested;
        bool _exitRequested;   // 면접 종료(리포트 생성 요청) 플래그
        bool _runStarted;      // Run()이 실제로 시작됐는지 여부

        string _lastUploadedS3Key;

        [Header("Replay")]
        public Button btnReplay;                         // 다시 듣기 버튼(캔버스에 추가 후 연결)
        [SerializeField, Range(1f, 20f)]
        float relistenWindowSeconds = 10f;               // 허용 시간(요청: 10초)

        AudioClip _lastAskedClip;                        // 직전에 재생한 질문/꼬리질문 음성
        bool _relistenRequested;                         // 버튼 클릭으로 재청취 요청됨
        bool _relistenUsedForThisQuestion;               // 이 문항에서 이미 1회 사용했는지

        [SerializeField] float askSafetyCapSeconds = 300f; // 0이면 무제한. 기본 5분 정도 권장
        [SerializeField] AudioSource askAudio;             // 질문/꼬리질문 재생용 AudioSource

        [System.Serializable]
        class EndInterviewReq
        {
            public int interviewId;
            public string s3Key;
        }

        bool _endPosted = false;
        int _interviewId;

        readonly List<float> _fullSamples = new();
        int _fullRate = 0;
        int _fullChannels = 0;
        bool _fullInit = false;

        void Start()
        {
            if (btnBack != null)
            {
                btnBack.onClick.RemoveAllListeners();
                btnBack.onClick.AddListener(OnBackButtonClicked);
            }


            if (btnStop != null)
            {
                btnStop.gameObject.SetActive(false);
                btnStop.onClick.RemoveAllListeners();
                btnStop.onClick.AddListener(() => _stopRequested = true);
            }

            if (btnReplay != null)
            {
                btnReplay.gameObject.SetActive(false);
                btnReplay.onClick.RemoveAllListeners();
                btnReplay.onClick.AddListener(() =>
                {
                    // 1문항당 1회만 허용
                    if (_relistenUsedForThisQuestion) return;

                    _relistenRequested = true;              // 루프를 즉시 빠져나가도록 신호
                    _relistenUsedForThisQuestion = true;    // 이후로는 비활성
                    btnReplay.interactable = false;
                    btnReplay.gameObject.SetActive(false);
                });
            }

            _session = new InterviewSession { sessionId = System.Guid.NewGuid().ToString(), prepSeconds = prepSeconds, answerSeconds = answerSeconds };

            if (demoGuidePanel) demoGuidePanel.SetActive(false);

            if (btnGuide)
            {
                btnGuide.gameObject.SetActive(false);
                btnGuide.onClick.RemoveAllListeners();
                btnGuide.onClick.AddListener(() =>
                {
                    if (demoGuidePanel) demoGuidePanel.SetActive(true);
                });
            }

            if (btnGuideClose)
            {
                btnGuideClose.onClick.RemoveAllListeners();
                btnGuideClose.onClick.AddListener(() =>
                {
                    if (demoGuidePanel) demoGuidePanel.SetActive(false);
                });
            }

            if (introPanel != null && btnIntroStart != null)
            {
                if (introTitleText != null)
                {
                    introTitleText.text = "면접 진행 안내";
                }

                if (introBodyText != null)
                {
                    introBodyText.text =
                        $"1. 질문이 끝나면 약 {prepSeconds:0}초 동안 답변을 준비할 수 있는 시간이 주어지며, 하단에 남은 시간이 표시됩니다.\n\n" +
                        $"2. 준비 시간이 끝나면 최대 {answerSeconds:0}초 동안 답변을 말할 수 있으며, 화면 아래 타이머를 통해 남은 시간을 확인할 수 있습니다.\n\n" +
                        $"3. '답변완료' 버튼으로 일찍 답변을 종료할 수 있습니다.\n\n" +
                        $"4. 답변이 시작된 뒤 약 {relistenWindowSeconds:0}초 동안은 각 질문마다 한 번씩 '다시 듣기' 버튼을 사용할 수 있습니다. 이 버튼을 누르면 현재 답변 녹음이 취소되고, 질문이 다시 재생된 뒤 짧은 준비 시간 후에 다시 답변을 진행합니다.\n\n" +
                        $"5. 준비가 되셨다면 아래 '면접 시작' 버튼을 눌러 면접을 진행해 주세요.";
                }

                // 안내 패널 표시
                introPanel.SetActive(true);

                // HUD에는 간단 안내만
                if (hudText != null)
                    hudText.text = "면접 진행 안내를 확인한 뒤, '면접 시작' 버튼을 눌러 주세요.";

                // 시작 버튼 클릭 시 안내 패널을 닫고 실제 면접 플로우 시작
                btnIntroStart.onClick.RemoveAllListeners();
                btnIntroStart.onClick.AddListener(() =>
                {
                    introPanel.SetActive(false);
                    Run().Forget();
                });
            }
            else
            {
                // 인트로 패널이 세팅되어 있지 않으면 기존처럼 바로 시작
                Run().Forget();
            }
        }

        void OnBackButtonClicked()
        {
            // Run()이 아직 시작되지 않았다면, 기존처럼 바로 결과 목록으로 이동
            if (!_runStarted)
            {
                SceneLoader.LoadSingleAsync(SceneIds.ResultsList).Forget();
                return;
            }

            // 이미 종료 처리 중이면 무시
            if (_exitRequested) return;

            _exitRequested = true;

            // 답변 녹음 중이었다면, 현재 답변을 강제로 종료해서 업로드까지 진행되도록 Stop 플래그만 세워준다
            if (mic != null && mic.IsRecording)
            {
                _stopRequested = true;
            }

            if (hudText != null)
            {
                hudText.text = "면접 종료 중입니다. 잠시만 기다려 주세요...";
            }
        }


        void ShowStopButton(bool show)
        {
            if (!btnStop) return;
            btnStop.gameObject.SetActive(show);
            btnStop.interactable = show;
        }


        async Task Run()
        {
            _runStarted = true;
            _state = InterviewState.Init;

            // 0) 먼저 interviewId 유효성 확인
            var interviewId = App.Presentation.Interview.InterviewStartContext.InterviewId;
            if (interviewId <= 0)
            {
                ShowBanner("면접 ID가 없습니다. 준비화면에서 질문 세트를 선택해주세요.");
                return;
            }

            // 1) 마이크 체크  (기존: string.IsNullOrEmpty(mic.selectedDevice) 만 검사 → 조기 종료)
            _state = InterviewState.DeviceCheck;

            var devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                ShowBanner("연결된 마이크가 없습니다. 장치를 연결한 뒤 다시 시도하세요.");
                return;
            }

            // 선택 장치가 없거나 현재 시스템에 없는 값이면 기본 장치 사용
            if (string.IsNullOrEmpty(mic.selectedDevice) || !devices.Contains(mic.selectedDevice))
            {
                // 빈 문자열이면 Microphone.Start가 기본 마이크를 사용합니다.
                mic.selectedDevice = string.Empty;
            }

            HideBanner();

            // 2) 질문 오디오(.wav) presigned 목록 가져와서 선 로드
            try
            {
                hudText.text = "질문 오디오를 불러오는 중...";
                Debug.Log($"[Interview] fetch presigned: interviewId={interviewId}");
                var items = await FetchPresignedQuestionUrls(interviewId);

                if (items == null || items.Length == 0)
                {
                    OpenFatal("재생할 질문이 없습니다.\n질문 세트를 다시 선택해주세요.");
                    return;
                }

                // 900초 만료 대비: 시작 시 전부 다운로드해 메모리에 보관
                foreach (var it in items)
                {
                    var clip = await DownloadWavClip(it.preSignedUrl);
                    if (clip == null) throw new System.Exception("오디오 다운로드 실패");

                    int label = MapDifficultToLabel(it.difficult);
                    _qclips.Add((it.interviewQuestionId, it.preSignedUrl, clip, it.difficult, label));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                OpenFatal("질문 오디오를 불러오지 못했습니다.\n네트워크 상태를 확인한 뒤 다시 시도해주세요.", ex: ex);
                return;
            }

            // 3) 질문별 루프
            for (_session.index = 0; _session.index < _qclips.Count; _session.index++)
            {
                if (_exitRequested) break;   // 종료 버튼이 눌렸으면 더 이상 새 질문으로 진입하지 않음

                var total = _qclips.Count;
                var q = _qclips[_session.index];

                _lastSpeakerLabel = q.speakerLabel;

                if (InterviewStartContext.Mode == InterviewMode.SelfIntro1Min && _session.index == 0)
                {
                    if (txtDemoScript) txtDemoScript.text = InterviewStartContext.DemoExampleText;
                    if (btnGuide) btnGuide.gameObject.SetActive(true);
                }
                else
                {
                    // 그 외에는 패널 숨기고 버튼도 숨김
                    if (demoGuidePanel) demoGuidePanel.SetActive(false);
                    if (btnGuide) btnGuide.gameObject.SetActive(false);
                }

                // 질문 오디오 재생
                _state = InterviewState.Asking;
                hudText.text = $"질문 {_session.index + 1}/{total}";
                await PlayClipExactAsync(q.clip, q.speakerLabel);

                // 준비 카운트다운
                _state = InterviewState.PrepCountdown;
                await Countdown(_session.prepSeconds, "곧 답변을 시작합니다... {0}s");

                var ac = await RecordAnswerPhaseWithRelisten(q.clip, q.speakerLabel);

                if (ac == null)
                {
                    ShowBanner("녹음에 실패했습니다. 마이크 설정을 확인해주세요.");
                    await Task.Delay(1200);
                    HideBanner();
                    continue;
                }

                AccumulateForFull(ac);

                // 로컬 저장만 수행
                _state = InterviewState.Uploading;
                hudText.text = "저장 중...";
                var wav = mic.ToWavBytes(ac);
                var baseName = $"{_session.sessionId}_q{_session.index + 1}";
                SaveWav(wav, baseName);

                string s3Key = null;

                // 업로드 presign → S3 PUT → 서버 등록
                try
                {
                    hudText.text = "업로드 준비 중...";
                    var fileName = baseName + ".wav";                 
                    var (putUrl, _s3Key) = await RequestUploadPresignAsync(fileName);
                    s3Key = _s3Key;     

                    hudText.text = "업로드 중...";
                    await UploadBytesToPresignedAsync(putUrl, wav, "audio/wav");
                    _lastUploadedS3Key = s3Key;

                    // ---- 꼬리질문 플로우 ----
                    if (App.Presentation.Interview.InterviewStartContext.Followups)
                    {
                        try
                        {
                            hudText.text = "꼬리질문 생성 중...";
                            // 메인 답변 업로드에 사용한 s3Key를 그대로 사용해서 꼬리질문 요청
                            // 예: try 블록 시작 전에 string s3Key = null; 로 선언해두고, presign 응답에서 대입.
                            // 아래는 s3Key 변수가 있다고 가정:
                            var (rqid, rurl, rLabel) = await RequestRelatedAsync(q.qid, s3Key);

                            _lastSpeakerLabel = rLabel;

                            hudText.text = $"질문 {_session.index + 1}의 꼬리질문";
                            var rclip = await DownloadWavClip(rurl);

                            // 꼬리질문 재생
                            _state = InterviewState.Asking;
                            await PlayClipExactAsync(rclip, rLabel);

                            // 짧은 준비시간
                            _state = InterviewState.PrepCountdown;
                            await Countdown(Mathf.Min(3f, _session.prepSeconds), "곧 답변을 시작합니다... {0}s");

                            var rac = await RecordAnswerPhaseWithRelisten(rclip, rLabel);
                            if (rac == null) throw new Exception("Follow-up recording failed.");

                            AccumulateForFull(rac);

                            // 저장 + 업로드 + 서버 등록(※ rqid 사용)
                            _state = InterviewState.Uploading;
                            var rwav = mic.ToWavBytes(rac);
                            var rbase = $"{_session.sessionId}_q{_session.index + 1}_followup";
                            SaveWav(rwav, rbase);

                            hudText.text = "업로드 준비 중...";
                            var (rPut, rKey) = await RequestUploadPresignAsync(rbase + ".wav");

                            hudText.text = "업로드 중...";
                            await UploadBytesToPresignedAsync(rPut, rwav, "audio/wav");

                            hudText.text = "서버 등록 중...";
                            await NotifyAnswerUploadedAsync(rqid, rKey);
                            Debug.Log($"[Interview] Follow-up uploaded & registered: qid={rqid}, s3Key={rKey}");
                            _lastUploadedS3Key = rKey;
                        }
                        catch (Exception ex)
                        {
                            // 실패 시 바로 다음 메인 문항으로 진행
                            Debug.LogWarning($"[Interview] Follow-up skipped: {ex.Message}");
                            ShowBanner("꼬리질문 생성/처리에 실패했습니다. 다음 질문으로 넘어갑니다.");
                            await Task.Delay(900);
                            HideBanner();
                        }
                    }
                    else
                    {
                        hudText.text = "서버 등록 중...";
                        await NotifyAnswerUploadedAsync(q.qid, s3Key);
                        Debug.Log($"[Interview] Uploaded & registered: qid={q.qid}, s3Key={s3Key}");
                    }

                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    ShowBanner("업로드에 실패했지만 로컬 저장은 완료되었습니다.");
                    await Task.Delay(1200);
                    HideBanner();
                }

                // 종료 버튼이 눌린 상태라면, 현재 문항까지 처리한 뒤 바로 루프를 탈출한다.
                if (_exitRequested)
                {
                    break;
                }

                // 다음 진행
                _state = InterviewState.NextOrEnd;
                hudText.text = "다음 질문으로 이동합니다...";
                await Task.Delay(400);
            }

            // 모든 문항 처리 완료 또는 중간 종료 → 종료 통지 및 결과 목록으로 이동
            hudText.text = "면접 종료 처리 중입니다. (리포트 생성 요청 중...)";

            // 이제는 합본 음성 업로드는 사용하지 않으므로,
            // 마지막으로 업로드에 성공한 답변 s3Key만 사용해서 종료 통지를 보낸다.
            // (_lastUploadedS3Key가 null일 수도 있으므로 그대로 전달)
            PostEndInterviewAsync(_lastUploadedS3Key).Forget();

            // 화면은 결과 목록으로 이동
            await SceneLoader.LoadSingleAsync(SceneIds.ResultsList);
        }

        int MapDifficultToLabel(string difficult)
        {
            if (string.IsNullOrEmpty(difficult)) return 0;

            switch (difficult.Trim().ToUpperInvariant())
            {
                case "STRICT":
                    return 0; // speakerA
                case "LAX":
                    return 1; // speakerB
                default:
                    Debug.LogWarning($"[Interview] Unknown difficult '{difficult}', defaulting to STRICT(A).");
                    return 0;
            }
        }

        // presigned 응답 DTO
        [System.Serializable]
        class PresignedItem
        {
            public int interviewQuestionId;
            public string preSignedUrl;
            public string difficult;
        }
        [System.Serializable] class ArrayEnvelope<T> { public bool success; public string message; public int code; public T[] data; }

        async Task<PresignedItem[]> FetchPresignedQuestionUrls(int interviewId)
        {
            _interviewId = interviewId;

            string path = $"/api/Interview/{interviewId}/questions/presigned-urls";
            var url = App.Infra.HttpClientBase.BaseUrl + path;

            // 자동 재발급 + 1회 재시도 포함
            var (status, text, result, error) = await App.Infra.HttpClientBase.GetAuto(url, auth: true);

            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new System.Exception($"GET {status} {error}\n{text}");

            var env = JsonUtility.FromJson<ArrayEnvelope<PresignedItem>>(text);
            if (env == null || env.data == null) return System.Array.Empty<PresignedItem>();

            return env.data;
        }

        async Task<AudioClip> DownloadWavClip(string url)
        {
            using var uwr = UnityWebRequest.Get(url);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            var op = uwr.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (uwr.result != UnityWebRequest.Result.Success || uwr.responseCode >= 400)
                throw new System.Exception($"Audio GET failed: {uwr.responseCode} {uwr.error}\n{uwr.downloadHandler?.text}");

            var data = uwr.downloadHandler.data;
            if (data == null || data.Length < 44)
                throw new System.Exception("Audio GET returned empty/too small body.");

            // 1) WAV 수동 디코드 시도(PCM16 또는 IEEE float32 지원)
            if (TryDecodeWav(data, out var clip))
            {
                clip.name = "InterviewQuestion";
                return clip;
            }

            // 2) 혹시 MP3/OGG인데 확장자만 .wav 인 경우를 대비한 폴백
            using (var uwr2 = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                var op2 = uwr2.SendWebRequest(); while (!op2.isDone) await Task.Yield();
                if (uwr2.result == UnityWebRequest.Result.Success)
                {
                    var c = DownloadHandlerAudioClip.GetContent(uwr2);
                    c.name = "InterviewQuestion";
                    return c;
                }
            }
            using (var uwr3 = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.OGGVORBIS))
            {
                var op3 = uwr3.SendWebRequest(); while (!op3.isDone) await Task.Yield();
                if (uwr3.result == UnityWebRequest.Result.Success)
                {
                    var c = DownloadHandlerAudioClip.GetContent(uwr3);
                    c.name = "InterviewQuestion";
                    return c;
                }
            }

            // 3) 디버깅용 덤프
            var dump = Path.Combine(Application.persistentDataPath, "bad_question_audio.bin");
            File.WriteAllBytes(dump, data);
            throw new System.Exception($"Unsupported audio format (not PCM16/Float32). Dumped: {dump}");
        }

        // ---- WAV 파서: PCM16 / IEEE Float32 지원 ----
        static bool TryDecodeWav(byte[] bytes, out AudioClip clip)
        {
            clip = null;
            // RIFF/WAVE 매직 체크
            if (Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF") return false;
            if (Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE") return false;

            int pos = 12;
            int channels = 1, sampleRate = 44100, bitsPerSample = 16, audioFormat = 1;
            int dataPos = -1, dataSize = 0;

            // chunk 탐색
            while (pos + 8 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, pos, 4);
                int size = System.BitConverter.ToInt32(bytes, pos + 4);
                pos += 8;

                if (id == "fmt ")
                {
                    audioFormat = System.BitConverter.ToInt16(bytes, pos + 0);   // 1=PCM, 3=float
                    channels = System.BitConverter.ToInt16(bytes, pos + 2);
                    sampleRate = System.BitConverter.ToInt32(bytes, pos + 4);
                    bitsPerSample = System.BitConverter.ToInt16(bytes, pos + 14);
                }
                else if (id == "data")
                {
                    dataPos = pos;
                    dataSize = size;
                    break;
                }
                pos += size;
            }
            if (dataPos < 0 || dataPos + dataSize > bytes.Length) return false;

            int frames; float[] samples;
            if (audioFormat == 1 && bitsPerSample == 16) // PCM16
            {
                frames = dataSize / 2;
                samples = new float[frames];
                int si = 0;
                for (int i = 0; i < dataSize; i += 2)
                {
                    short s = System.BitConverter.ToInt16(bytes, dataPos + i);
                    samples[si++] = s / 32768f;
                }
                frames /= channels;
            }
            else if (audioFormat == 3 && bitsPerSample == 32) // IEEE float32
            {
                frames = dataSize / 4;
                samples = new float[frames];
                Buffer.BlockCopy(bytes, dataPos, samples, 0, dataSize);
                frames /= channels;
            }
            else
            {
                return false; // 다른 포맷(ALaw/μLaw/PCM24 등) → 지원 안함
            }

            clip = AudioClip.Create("WAV", frames, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return true;
        }

        async Task WaitWhilePlaying()
        {
            // 질문 음성 재생이 모두 끝날 때까지 대기
            while (voiceSource != null && voiceSource.isPlaying)
                await Task.Yield();
        }

        async Task PlayClipWithTimeout(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[Interview] Null clip. Skipping play.");
                return;
            }

            // 안전 설정
            voiceSource.loop = false;         // ❗ 무한재생 방지
            voiceSource.spatialBlend = 0f;    // 2D로 (3D 공간에서 안 들리는 경우 방지)
            voiceSource.Stop();
            voiceSource.clip = clip;

            Debug.Log($"[Interview] Play Q{_session.index + 1}: len={clip.length:F2}s, ch={clip.channels}, rate={clip.frequency}");

            // 오디오 리스너가 없으면 임시로 추가(소리는 안나도 진행은 하도록)
            if (FindObjectOfType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            voiceSource.Play();

            // 길이에 기반해 대기하되, 상한/하한을 둡니다.
            float expected = Mathf.Max(0.01f, clip.length);
            float timeout = Mathf.Clamp(expected * 1.5f, 3f, 30f);  // 최소 3초, 최대 30초
            float start = Time.realtimeSinceStartup;

            while (voiceSource != null && voiceSource.isPlaying)
            {
                if (Time.realtimeSinceStartup - start > timeout)
                {
                    Debug.LogWarning($"[Interview] Audio timeout reached ({timeout:F1}s). Forcing continue.");
                    voiceSource.Stop();
                    break;
                }
                await System.Threading.Tasks.Task.Yield();
            }
        }


        async Task Countdown(float seconds, string fmt)
        {
            float t = seconds;
            while (t > 0f)
            {
                hudText.text = string.Format(fmt, Mathf.CeilToInt(t));
                await Task.Yield();
                t -= Time.deltaTime;
            }
        }

        InterviewSet DummySet() => new InterviewSet
        {
            id = "dummy",
            title = "기본 세트",
            items = new[]{
                new InterviewQuestion{ id="q1", text="자기소개를 해주세요."},
                new InterviewQuestion{ id="q2", text="가장 도전적인 프로젝트와 역할은 무엇이었나요?"},
                new InterviewQuestion{ id="q3", text="최근 협업에서 갈등을 어떻게 해결했나요?"}
            }
        };

        void ShowBanner(string msg)
        {
            if (bannerText == null) return;
            bannerText.text = msg;
            bannerText.gameObject.SetActive(true);
        }

        void HideBanner()
        {
            if (bannerText == null) return;
            bannerText.gameObject.SetActive(false);
        }

        void SetupUIMode()
        {
            var mode = ResolveMode(startMode);
            bool useDesktop = (mode == UIMode.Desktop);

            // 캔버스 토글
            if (desktopCanvas) desktopCanvas.SetActive(useDesktop);
            if (vrCanvas) vrCanvas.SetActive(!useDesktop);

            // 이벤트 모듈 토글(둘 다 켜두면 충돌)
            if (desktopUIModule) desktopUIModule.enabled = useDesktop;
            if (xrUIModule) xrUIModule.enabled = !useDesktop;
        }

        UIMode ResolveMode(UIMode mode)
        {
            if (mode != UIMode.Auto) return mode;
            // 간단한 휴리스틱: HMD 활성 시 VR, 아니면 Desktop
            return XRSettings.isDeviceActive ? UIMode.VR : UIMode.Desktop;
        }

        string SaveWav(byte[] wavBytes, string fileBaseName)
        {
            // Editor/빌드 공통 안전 경로
            string dir = Path.Combine(Application.persistentDataPath, recordingsFolder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileBaseName + ".wav");
            File.WriteAllBytes(path, wavBytes);
            Debug.Log($"[Interview] Saved WAV: {path}");
            return path;
        }

        // 업로드 presign 응답 DTO
        [Serializable] class UploadPresignEnv { public bool success; public string message; public int code; public UploadPresignData data; }
        [Serializable] class UploadPresignData { public string presignedUrl; public string s3Key; public long expirationSeconds; }

        // presign(업로드용) 요청 → url, s3Key 반환
        async Task<(string url, string s3Key)> RequestUploadPresignAsync(string fileName)
        {
            string path = $"/api/Interview/presigned-url?fileName={UnityWebRequest.EscapeURL(fileName)}";
            var url = App.Infra.HttpClientBase.BaseUrl + path;

            var (status, text, result, error) = await App.Infra.HttpClientBase.PostJsonAuto(url, json: null, auth: true);
            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new Exception($"PRESIGN POST {status} {error}\n{text}");

            var env = JsonUtility.FromJson<UploadPresignEnv>(text);
            if (env == null || env.data == null || string.IsNullOrEmpty(env.data.presignedUrl))
                throw new Exception("Invalid presign response.");

            return (env.data.presignedUrl, env.data.s3Key);
        }

        // S3 presigned URL로 WAV 바이트 PUT
        async Task UploadBytesToPresignedAsync(string presignedUrl, byte[] data, string contentType = "audio/wav")
        {
            using var req = new UnityWebRequest(presignedUrl, UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerRaw(data) { contentType = contentType };
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", contentType);
            req.chunkedTransfer = false;       // 서명 불일치 방지
            req.useHttpContinue = false;
            req.timeout = Math.Max(60, data.Length / (1024 * 1024) * 2 + 30);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            // S3는 200 또는 204를 돌려줄 수 있음
            if (req.result != UnityWebRequest.Result.Success || (req.responseCode != 200 && req.responseCode != 204))
                throw new Exception($"S3 PUT failed: {(long)req.responseCode} {req.error}\n{req.downloadHandler?.text}");
        }

        // 서버에 “이 질문의 답변이 s3Key로 업로드됨” 등록
        async Task NotifyAnswerUploadedAsync(int questionId, string s3Key)
        {
            string path = "/api/Interview/reply";
            var url = App.Infra.HttpClientBase.BaseUrl + path;

            var body = JsonUtility.ToJson(new RelatedReq { questionId = questionId, s3Key = s3Key });
            Debug.Log($"[Interview] FOLLOW-UP REGISTER → {body}");

            var (status, text, result, error) =
                await App.Infra.HttpClientBase.PostJsonAuto(url, body, auth: true);

            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new Exception($"FOLLOW-UP REGISTER POST {status} {error}\n{text}");

            Debug.Log($"[Interview] FOLLOW-UP REGISTER OK ← {status}");
        }

        [Serializable] class PostBody { public int questionId; public string s3Key; }

        [Serializable] class RelatedReq { public int questionId; public string s3Key; }
        [Serializable] class RelatedEnv { public bool success; public string message; public int code; public RelatedData data; }
        [Serializable]
        class RelatedData
        {
            public int questionId;
            public string preSignedUrl;
            public string difficult;   // "STRICT" / "LAX" (없으면 null 또는 빈 문자열)
        }

        async Task<(int qid, string url, int speakerLabel)> RequestRelatedAsync(int questionId, string s3Key)
        {
            string path = "/api/Interview/related";
            var url = App.Infra.HttpClientBase.BaseUrl + path;

            var body = JsonUtility.ToJson(new RelatedReq { questionId = questionId, s3Key = s3Key });
            var (status, text, result, error) = await App.Infra.HttpClientBase.PostJsonAuto(url, body, auth: true);
            if (result != UnityWebRequest.Result.Success || status >= 400)
                throw new Exception($"RELATED POST {status} {error}\n{text}");

            var env = JsonUtility.FromJson<RelatedEnv>(text);
            if (env?.data == null || string.IsNullOrEmpty(env.data.preSignedUrl))
                throw new Exception("Invalid related response.");

            // 응답에 difficult가 오면 그 값으로 화자 결정,
            // 없으면 이전 질문의 화자(_lastSpeakerLabel)를 그대로 사용
            int label = _lastSpeakerLabel;
            if (!string.IsNullOrEmpty(env.data.difficult))
            {
                label = MapDifficultToLabel(env.data.difficult);
            }

            return (env.data.questionId, env.data.preSignedUrl, label);
        }

        void OpenFatal(string body, string title = "질문 다운로드 실패", bool showRetry = false, Exception ex = null)
        {
            string detail = body;
            if (ex != null) detail += $"\n\n({ex.Message})";

            // 1차 버튼: 면접 준비 화면으로
            System.Action goBack = async () => { await SceneLoader.LoadSingleAsync(SceneIds.InterviewPrepare); };

            // (선택) 2차 버튼: 다시 시도 → 이 씬을 리로드
            System.Action retry = null;
            if (showRetry) retry = async () => { await SceneLoader.LoadSingleAsync(SceneIds.Interview); };

            fatalModal.Open(title, detail, goBack, "면접 준비로", retry, "다시 시도");
        }

        async System.Threading.Tasks.Task PostEndInterviewAsync(string fullS3Key)
        {
            if (_endPosted || _interviewId <= 0) return;

            try
            {
                var path = "/api/Interview/end";
                var url = App.Infra.HttpClientBase.BaseUrl + path;
                var body = JsonUtility.ToJson(new EndInterviewReq { interviewId = _interviewId, s3Key = fullS3Key });

                Debug.Log($"[Interview] END POST → body={body}");
                var (status, text, result, error) = await App.Infra.HttpClientBase.PostJsonAuto(url, body, auth: true);

                if (result != UnityWebRequest.Result.Success || status >= 400)
                    Debug.LogWarning($"[Interview] END RESP ← {status} {error}\n{text}");
                else
                {
                    Debug.Log($"[Interview] END OK ← {status} {text}");
                    _endPosted = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Interview] END POST failed: {ex.Message}");
            }
        }


        void AccumulateForFull(AudioClip clip, float gapSeconds = 0.25f)
        {
            if (clip == null) return;

            if (!_fullInit)
            {
                _fullRate = clip.frequency;
                _fullChannels = clip.channels;
                _fullInit = true;
            }

            // 샘플 추출
            var frameCount = clip.samples;
            var tmp = new float[frameCount * clip.channels];
            clip.GetData(tmp, 0);
            _fullSamples.AddRange(tmp);

            // 구간 사이 약간의 무음(선택)
            if (gapSeconds > 0f && _fullRate > 0 && _fullChannels > 0)
            {
                int gap = Mathf.RoundToInt(gapSeconds * _fullRate * _fullChannels);
                for (int i = 0; i < gap; i++) _fullSamples.Add(0f);
            }
        }

        byte[] BuildFullInterviewWavBytes()
        {
            if (!_fullInit || _fullSamples.Count == 0) return null;

            int frames = _fullSamples.Count / _fullChannels;
            var clip = AudioClip.Create("InterviewFull", frames, _fullChannels, _fullRate, false);
            clip.SetData(_fullSamples.ToArray(), 0);

            return mic.ToWavBytes(clip);
        }

        async Task<string> UploadFullInterviewIfAnyAsync()
        {
            var bytes = BuildFullInterviewWavBytes();
            if (bytes == null) return _lastUploadedS3Key;

            var baseName = $"{_session.sessionId}_interview_full";
            // SaveWav이 파일 경로를 반환하도록 이미 구현되어 있으니 그 값을 재사용
            string path = SaveWav(bytes, baseName);

            // (선택) 너무 크면 합본 업로드는 생략하고 null 리턴 — 서버는 개별 문항들로 리포트 가능
            long size = new System.IO.FileInfo(path).Length;
            const long LIMIT = 100L * 1024 * 1024;
            if (size > LIMIT)
            {
                Debug.LogWarning($"[Interview] Full interview is too large ({size / (1024 * 1024)}MB). Skip uploading full.");
                return _lastUploadedS3Key;
            }

            try
            {
                var (putUrl, s3Key) = await RequestUploadPresignAsync(System.IO.Path.GetFileName(path));
                await UploadFileToPresignedAsync(putUrl, path, "audio/wav");
                Debug.Log($"[Interview] Full interview uploaded: key={s3Key}, size={size / (1024f * 1024f):F1}MB");

                // 합본 성공 시 그 키를 반환(우선 사용)
                return s3Key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interview] Full upload failed → fallback last key. {ex.Message}");
                return _lastUploadedS3Key; // 실패 시 마지막 개별 키로 대체
            }
        }

        async Task UploadFileToPresignedAsync(string presignedUrl, string filePath, string contentType = "audio/wav")
        {
            using var req = new UnityWebRequest(presignedUrl, UnityWebRequest.kHttpVerbPUT);
            req.uploadHandler = new UploadHandlerFile(filePath);
            req.downloadHandler = new DownloadHandlerBuffer();

            // S3 서명과 맞추려면 Content-Type 헤더도 동일해야 하는 경우가 많습니다.
            req.SetRequestHeader("Content-Type", contentType);

            // S3 presigned 특성상 chunked 금지 + 100-continue 금지
            req.chunkedTransfer = false;
            req.useHttpContinue = false;

            // 큰 파일 대비 타임아웃 여유(네트워크 상황 따라 조정)
            var len = new System.IO.FileInfo(filePath).Length;
            // 최소 120초, 대략 1MB당 1.5초 가정(업로드 속도 느린 환경 대비)
            req.timeout = Mathf.Max(120, (int)(len / (1024 * 1024) * 1.5f) + 30);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success || (req.responseCode != 200 && req.responseCode != 204))
                throw new System.Exception($"S3 PUT(file) failed: {(long)req.responseCode} {req.error}\n{req.downloadHandler?.text}");
        }

        async Task BackgroundFinishAsync()
        {
            string keyToSend = null;

            try
            {
                // 합본 업로드 시도 → 실패/초과 시 마지막 개별 키 반환
                keyToSend = await UploadFullInterviewIfAnyAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interview] Background full upload error: {ex.Message}");
                // 그래도 마지막 개별 키를 시도(없으면 null)
                keyToSend = _lastUploadedS3Key;
            }

            try
            {
                // 최소한 null이 아니라면 종료 통지 요청
                if (!string.IsNullOrEmpty(keyToSend))
                {
                    await PostEndInterviewAsync(keyToSend);
                }
                else
                {
                    // 정말로 개별 업로드도 없었다면(드문 케이스)
                    await PostEndInterviewAsync(null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interview] Background end-post error: {ex.Message}");
            }
        }

        void ArmRelistenWindow(AudioClip askedClip, float seconds)
        {
            _lastAskedClip = askedClip;
            _relistenRequested = false;

            if (btnReplay == null) return;

            btnReplay.gameObject.SetActive(true);
            btnReplay.interactable = true;

            // seconds 경과 또는 녹음 종료 시 자동 비활성
            AutoDisableRelisten(seconds).Forget();
        }

        async Task AutoDisableRelisten(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (mic != null && mic.IsRecording && !_relistenRequested &&
                   (Time.realtimeSinceStartup - start) < seconds)
            {
                await Task.Yield();
            }
            if (btnReplay != null)
            {
                btnReplay.interactable = false;
                btnReplay.gameObject.SetActive(false);
            }
        }

        void CancelRelistenWindow()
        {
            if (btnReplay == null) return;
            btnReplay.interactable = false;
            btnReplay.gameObject.SetActive(false);
        }

        // askedClip: 방금 재생한 질문(or 꼬리질문) 클립
        async Task<AudioClip> RecordAnswerPhaseWithRelisten(AudioClip askedClip, int speakerLabel)
        {
            _relistenRequested = false;
            _relistenUsedForThisQuestion = false;

        RETRY: // ⇦ 재청취 1회 허용을 위한 재시작 라벨
            _state = InterviewState.Recording;
            progressBar.value = 0f;

            mic.StopMetering();
            mic.StartRecord();

            ShowStopButton(true);
            hudText.text = "답변해주세요";

            // 10초 재청취 허용 창을 연다
            ArmRelistenWindow(askedClip, relistenWindowSeconds);

            float t = 0f;
            _stopRequested = false;
            while (t < _session.answerSeconds && !_stopRequested && !_relistenRequested)
            {
                await Task.Yield();
                t += Time.deltaTime;
                progressBar.value = t / _session.answerSeconds;

                if (Input.GetKeyDown(KeyCode.Space)) _stopRequested = true; // 기존 스페이스 처리 유지
            }

            ShowStopButton(false);
            var recorded = mic.StopRecord(recordingTailSeconds);
            CancelRelistenWindow(); // 창 닫기

            // 재청취가 요청되었으면 방금 녹음은 폐기하고(미사용),
            // 질문음을 다시 재생 + 짧은 준비시간 후 재녹음으로 1회만 돌아간다.
            if (_relistenRequested)
            {
                // (방금 녹음된 'recorded'는 사용하지 않음)
                _relistenRequested = false;

                // 질문/꼬리질문을 다시 재생
                _state = InterviewState.Asking;
                await PlayClipExactAsync(askedClip, speakerLabel);

                hudText.text = "면접 질문을 다시 재생합니다";

                // 짧은 준비 카운트다운(기존 prepSeconds와 동일하거나 2~3초 권장)
                _state = InterviewState.PrepCountdown;
                await Countdown(Mathf.Min(3f, _session.prepSeconds), "곧 답변을 시작합니다... {0}s");

                // 1회만 허용 → 이미 true로 세팅되어 있고, 버튼도 꺼진 상태
                // 다시 녹음으로 돌아간다
                goto RETRY;
            }

            // 재청취 없이 정상 종료
            return recorded;
        }

        async Task PlayClipExactAsync(AudioClip clip, int? speakerLabel = null, float? safetyCapSeconds = null)
        {
            if (!clip) return;

            // 길이 계산: samples/frequency가 가장 정확
            double dur = 0d;
            if (clip.frequency > 0 && clip.samples > 0) dur = (double)clip.samples / clip.frequency;
            if (dur <= 0d) dur = Math.Max(0.1d, clip.length);

            // 안전 상한
            double cap = safetyCapSeconds.HasValue
                ? Math.Max(0.0, safetyCapSeconds.Value)
                : (askSafetyCapSeconds > 0 ? askSafetyCapSeconds : double.MaxValue);

            // DSP 기준으로 종료 시점 계산
            double startDsp = AudioSettings.dspTime;
            double expectedEnd = startDsp + dur + 0.05d;
            double hardEnd = Math.Min(expectedEnd, startDsp + cap);

            // 1) LipSyncRouter가 있고, 라벨이 지정된 경우 → 화자별 립싱크/오디오로 재생
            if (lipSyncRouter != null && speakerLabel.HasValue)
            {
                lipSyncRouter.PlayLabeledClip(clip, speakerLabel.Value);

                // 지정된 길이만큼 대기
                while (AudioSettings.dspTime < hardEnd)
                    await System.Threading.Tasks.Task.Yield();

                return;
            }

            // 2) 기존 AudioSource 경로(단일 스피커) - LipSyncRouter 미설정 시 fallback
            var src = askAudio ?? voiceSource ?? GetComponent<AudioSource>();
            if (!src)
            {
                Debug.LogWarning("[Interview] No AudioSource found. Playing via LipSync only or skipping audio.");
                while (AudioSettings.dspTime < hardEnd)
                    await System.Threading.Tasks.Task.Yield();
                return;
            }

            // 준비
            src.Stop();
            src.loop = false;
            src.clip = clip;
            src.time = 0f;

            src.Play();

            // 재생 종료까지 대기(외부에서 중지해도 빠져나오도록)
            while (src.isPlaying && AudioSettings.dspTime < hardEnd)
                await System.Threading.Tasks.Task.Yield();

            // 안전 차단으로 끝났으면 정지
            if (src.isPlaying) src.Stop();
        }


    }

    static class TaskExt { public static async void Forget(this Task t) { try { await t; } catch (System.Exception e) { UnityEngine.Debug.LogException(e); } } }
}
