using System.Threading.Tasks;
namespace App.Services
{
    /// <summary>업로드 딜레이만 모사하여 고정 텍스트를 반환하는 로컬 STT 대체 구현.</summary>

    public class DummySttService : ISttService
    {
        public async Task<string> UploadAndTranscribe(byte[] wavBytes, int sampleRate)
        {
            await Task.Delay(400); // 업로드/처리 대기 흉내
            return "(더미) 입력 음성 → 텍스트 변환 결과";
        }
    }
}
