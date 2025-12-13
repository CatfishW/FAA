using UnityEngine;

public class EditorFunctionScene : MonoBehaviour
{
    [SerializeField] private bool callAtRuntime = false;
    public UnityEngine.Events.UnityEvent functionToCall;

    private void Awake()
    {
        if (!callAtRuntime)
            this.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (callAtRuntime)
            functionToCall.Invoke();
    }
}
