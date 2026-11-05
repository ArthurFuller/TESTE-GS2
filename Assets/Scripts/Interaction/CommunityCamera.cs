using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Camera), typeof(CinemachineBrain))]
public sealed class CommunityCamera : MonoBehaviour
{
    [SerializeField] private CinemachineCamera communityShot;
    [SerializeField] private CinemachineCamera workerShot;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private Transform minimumCorner;
    [SerializeField] private Transform maximumCorner;
    [SerializeField] private bool useAdditionalPositionLimits;
    [SerializeField] private bool useCustomViewBounds;
    [SerializeField] private Vector2 minimumView;
    [SerializeField] private Vector2 maximumView;
    [SerializeField] private Button communityButton;
    [SerializeField, Min(0.1f)] private float panSpeed = 6f;
    [SerializeField, Min(0.1f)] private float zoomStep = 0.8f;
    [SerializeField, Min(0.1f)] private float keyboardZoomSpeed = 4f;
    [SerializeField, Min(0.1f)] private float closestDistance = 7.5f;
    [SerializeField, Min(0.1f)] private float farthestDistance = 16f;
    [SerializeField, Min(0.1f)] private float followDistance = 8.7f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.35f;
    [SerializeField, Min(0f)] private float farZoomPanRadius = 2f;

    private Transform followed;
    private Vector3 manualFocus;
    private Vector3 homeFocus;
    private Vector3 homePosition;
    private Vector3 velocity;
    private Vector3 followVelocity;
    private float manualDistance;
    private float homeDistance;
    private bool manualActive;
    private bool followLocked;
    private bool heldView;
    private readonly Vector2[] outerView = new Vector2[4];
    private readonly Vector2[] currentView = new Vector2[4];

    public Transform ViewTransform => communityShot != null ? communityShot.transform : transform;
    public CinemachineCamera CommunityShot => communityShot;
    public float FieldOfView
    {
        get => communityShot != null ? communityShot.Lens.FieldOfView : GetComponent<Camera>().fieldOfView;
        set
        {
            if (communityShot != null)
            {
                LensSettings lens = communityShot.Lens;
                lens.FieldOfView = value;
                communityShot.Lens = lens;
            }
            else GetComponent<Camera>().fieldOfView = value;
        }
    }
    public bool IsFollowing(Transform worker) => followed == worker;
    public bool IsFollowReady => followed != null && workerShot != null && brain != null &&
        ReferenceEquals(brain.ActiveVirtualCamera, workerShot) && !brain.IsBlending &&
        Vector3.Distance(workerShot.transform.position,
            followed.position - workerShot.transform.forward * followDistance) < 0.03f;

    private void Awake()
    {
        if (communityShot != null) communityShot.Priority.Value = 10;
        if (workerShot != null) workerShot.Priority.Value = 0;
        UseCurrentViewAsHome();
        if (communityButton != null)
            communityButton.onClick.AddListener(ReturnToCommunity);
    }

    public void UseCurrentViewAsHome()
    {
        Transform view = ViewTransform;
        homePosition = view.position;
        homeDistance = view.position.y / -view.forward.y;
        homeFocus = view.position + view.forward * homeDistance;
        manualDistance = homeDistance;
        manualFocus = homeFocus;
        manualActive = false;
        followed = null;
        followLocked = false;
        heldView = false;
        velocity = Vector3.zero;
        if (workerShot != null) workerShot.Priority.Value = 0;
        if (communityButton != null)
            communityButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (communityButton != null) communityButton.onClick.RemoveListener(ReturnToCommunity);
    }

    private void Update()
    {
        if (heldView) return;
        Keyboard keyboard = Keyboard.current;
        Vector2 movement = Vector2.zero;
        float zoom = 0f;
        if (keyboard != null)
        {
            movement.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            movement.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            zoom = ((keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed ? 1 : 0) -
                (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed ? 1 : 0)) *
                Time.deltaTime * keyboardZoomSpeed;
        }
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0) zoom += Mathf.Sign(scroll);
        }

        if (followed != null && followLocked) return;
        if (followed != null && (movement != Vector2.zero || zoom != 0))
        {
            ReturnToCommunity();
            return;
        }
        if (followed != null) return;
        if (movement == Vector2.zero && zoom == 0) return;

        manualActive = true;
        manualFocus += new Vector3(movement.x, 0, movement.y).normalized * panSpeed * Time.deltaTime;
        if (zoom != 0)
            manualDistance = Mathf.Clamp(manualDistance - zoom * zoomStep,
                closestDistance, farthestDistance);
        ClampManualView();
    }

    private void LateUpdate()
    {
        if (followed != null && workerShot != null)
        {
            Transform shot = workerShot.transform;
            Vector3 target = followed.position - shot.forward * followDistance;
            shot.position = Vector3.SmoothDamp(shot.position, target, ref followVelocity, smoothTime);
        }
        if (!manualActive) return;
        Transform view = ViewTransform;
        Vector3 communityTarget = manualFocus - view.forward * manualDistance;
        // O alvo já é limitado em Update. Limitar novamente cada passo do
        // SmoothDamp causa um salto quando a área visível cresce no zoom out.
        view.position = Vector3.SmoothDamp(view.position,
            communityTarget, ref velocity, smoothTime);
    }

    public void Follow(Transform worker)
    {
        if (heldView || worker == null || workerShot == null) return;
        bool switchingWorker = followed != null;
        followed = worker;
        followLocked = false;
        manualActive = false;
        if (!switchingWorker)
            workerShot.transform.position = worker.position - workerShot.transform.forward * followDistance;
        followVelocity = Vector3.zero;
        workerShot.Priority.Value = 20;
        if (communityButton != null) communityButton.gameObject.SetActive(true);
    }

    public void ReturnToCommunity()
    {
        if (followed == null && !heldView || followLocked) return;
        heldView = false;
        followed = null;
        manualActive = false;
        manualFocus = homeFocus;
        manualDistance = homeDistance;
        ViewTransform.position = homePosition;
        if (workerShot != null) workerShot.Priority.Value = 0;
        if (communityButton != null) communityButton.gameObject.SetActive(false);
    }

    public void HoldCurrentView()
    {
        if (workerShot == null) return;
        followed = null;
        followLocked = false;
        heldView = true;
        workerShot.Priority.Value = 20;
        if (communityButton != null) communityButton.gameObject.SetActive(false);
    }

    public void SetFollowLocked(bool locked)
    {
        followLocked = locked;
        if (communityButton != null)
            communityButton.gameObject.SetActive(followed != null && !locked);
    }

    private void ClampManualView()
    {
        Vector3 forward = ViewTransform.forward;
        Vector3 position = manualFocus - forward * manualDistance;
        manualFocus = ClampPosition(position) + forward * manualDistance;
    }

    private Vector3 ClampPosition(Vector3 position)
    {
        if (!GetViewBounds(position, out Vector2 outerMin, out Vector2 outerMax,
            out Vector2 currentMin, out Vector2 currentMax))
        {
            position.x = homePosition.x;
            position.z = homePosition.z;
            return position;
        }

        Vector2 center = new Vector2(position.x, position.z);
        Vector2 min = outerMin - (currentMin - center);
        Vector2 max = outerMax - (currentMax - center);
        bool extra = useAdditionalPositionLimits && minimumCorner != null && maximumCorner != null;
        position.x = ClampAxis(position.x, min.x, max.x,
            extra ? minimumCorner.position.x : float.NegativeInfinity,
            extra ? maximumCorner.position.x : float.PositiveInfinity);
        position.z = ClampAxis(position.z, min.y, max.y,
            extra ? minimumCorner.position.z : float.NegativeInfinity,
            extra ? maximumCorner.position.z : float.PositiveInfinity);
        return position;
    }

    private static float ClampAxis(float value, float min, float max, float extraMin, float extraMax)
    {
        if (min > max) return (min + max) * 0.5f;
        float combinedMin = Mathf.Max(min, extraMin);
        float combinedMax = Mathf.Min(max, extraMax);
        if (combinedMin <= combinedMax)
        {
            min = combinedMin;
            max = combinedMax;
        }
        return Mathf.Clamp(value, min, max);
    }

    public bool GetAutomaticViewBounds(out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        Transform view = ViewTransform;
        if (-view.forward.y < 0.01f) return false;
        Vector3 home = Application.isPlaying ? homePosition : view.position;
        float homeDistanceToGround = home.y / -view.forward.y;
        Vector3 farPosition = home + view.forward * (homeDistanceToGround - farthestDistance);
        if (!GetGroundView(farPosition, outerView)) return false;
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            min = Vector2.Min(min, outerView[i]);
            max = Vector2.Max(max, outerView[i]);
        }
        min -= Vector2.one * farZoomPanRadius;
        max += Vector2.one * farZoomPanRadius;
        return true;
    }

    private bool GetViewBounds(Vector3 position, out Vector2 outerMin, out Vector2 outerMax,
        out Vector2 currentMin, out Vector2 currentMax)
    {
        outerMin = currentMin = Vector2.zero;
        outerMax = currentMax = Vector2.zero;
        if (!GetAutomaticViewBounds(out outerMin, out outerMax) ||
            !GetGroundView(position, currentView)) return false;
        if (useCustomViewBounds && minimumView.x < maximumView.x &&
            minimumView.y < maximumView.y)
        {
            outerMin = minimumView;
            outerMax = maximumView;
        }
        currentMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        currentMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            currentMin = Vector2.Min(currentMin, currentView[i]);
            currentMax = Vector2.Max(currentMax, currentView[i]);
        }
        return true;
    }

    private bool GetGroundView(Vector3 position, Vector2[] corners)
    {
        Camera sceneCamera = GetComponent<Camera>();
        Quaternion rotation = ViewTransform.rotation;
        float height = Mathf.Tan(FieldOfView * Mathf.Deg2Rad * 0.5f);
        float width = height * sceneCamera.aspect;
        for (int i = 0; i < 4; i++)
        {
            float x = i == 0 || i == 3 ? -width : width;
            float y = i < 2 ? -height : height;
            Vector3 direction = rotation * new Vector3(x, y, 1f);
            if (direction.y >= -0.001f) return false;
            Vector3 point = position + direction * (-position.y / direction.y);
            corners[i] = new Vector2(point.x, point.z);
        }
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!GetViewBounds(ViewTransform.position, out Vector2 outerMin, out Vector2 outerMax,
            out Vector2 currentMin, out Vector2 currentMax)) return;
        Gizmos.color = new Color(0f, 0.65f, 1f, 0.95f);
        DrawRectangle(outerMin, outerMax, 0.05f);
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.85f);
        DrawRectangle(currentMin, currentMax, 0.08f);
    }

    private static void DrawRectangle(Vector2 min, Vector2 max, float height)
    {
        Gizmos.DrawWireCube(new Vector3((min.x + max.x) * 0.5f, height,
            (min.y + max.y) * 0.5f), new Vector3(max.x - min.x, 0, max.y - min.y));
    }
}
