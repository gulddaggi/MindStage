namespace App.Presentation.Interview
{
    /// <summary>면접 진행 흐름의 상태값을 정의.</summary>

    public enum InterviewState
    {
        Init, 
        DeviceCheck, 
        SelectSetOrSkip,
        Asking, 
        PrepCountdown, 
        Recording, 
        Uploading, 
        FollowUpOptional, 
        NextOrEnd
    }
}
