namespace App.Services
{
    /// <summary>녹음 음성을 업로드해 텍스트로 변환하는 STT 서비스 인터페이스.</summary>

    public interface ISttService
    {
        // 실제에선 서버로 업로드하고 텍스트를 받음. 지금은 비동기 딜레이 후 더미 텍스트
        System.Threading.Tasks.Task<string> UploadAndTranscribe(byte[] wavBytes, int sampleRate);
    }
}
