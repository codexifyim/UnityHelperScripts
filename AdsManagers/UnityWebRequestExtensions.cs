using System.Threading.Tasks;
using UnityEngine.Networking;

public static class UnityWebRequestExtensions
{
    public static Task<UnityWebRequest> SendWebRequestAsync(this UnityWebRequest webRequest)
    {
        var tcs = new TaskCompletionSource<UnityWebRequest>();
        var operation = webRequest.SendWebRequest();

        operation.completed += _ =>
        {
            tcs.SetResult(webRequest);
        };

        return tcs.Task;
    }
}
