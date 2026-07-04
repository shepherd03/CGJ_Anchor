using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 控制弹幕在 UI 屏幕区域内的生成、移动参数和生命周期。
/// </summary>
[DisallowMultipleComponent]
public class BulletScreenController : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField, Tooltip("单条弹幕预制体，运行时会挂到 Root 下生成。")]
    private GameObject bulletPrefab;

    [SerializeField, Tooltip("弹幕生成父节点；留空时使用当前物体的 RectTransform。")]
    private RectTransform bulletRoot;

    [Header("Spawn")]
    [SerializeField, Tooltip("启用时是否自动持续生成弹幕。")]
    private bool playOnEnable = true;

    [SerializeField, Min(0.05f), Tooltip("两条自动弹幕之间的最短间隔。")]
    private float minSpawnInterval = 0.45f;

    [SerializeField, Min(0.05f), Tooltip("两条自动弹幕之间的最长间隔。")]
    private float maxSpawnInterval = 1.2f;

    [Header("Pool")]
    [FormerlySerializedAs("maxActiveBullets")]
    [SerializeField, Min(1), Tooltip("对象池最大实例数量，同时也是屏幕上最多同时存在的弹幕数量。")]
    private int poolCapacity = 40;

    [SerializeField, Tooltip("Awake 时是否提前创建完整对象池，减少运行中 Instantiate。")]
    private bool prewarmPoolOnAwake = true;

    [SerializeField, Min(0f), Tooltip("以 Root 中心为基准，弹幕 Y 轴向上随机的最大距离。")]
    private float rootUpDistance = 300f;

    [SerializeField, Min(0f), Tooltip("以 Root 中心为基准，弹幕 Y 轴向下随机的最大距离。")]
    private float rootDownDistance = 300f;

    [SerializeField, Range(0f, 1f), Tooltip("弹幕生成在 Root 上半部分的概率，0.7 表示 70%。")]
    private float upperHalfSpawnChance = 0.7f;

    [SerializeField, Min(0f), Tooltip("弹幕生成时距离右侧屏幕外的额外距离。")]
    private float rightSpawnPadding = 80f;

    [SerializeField, Min(0f), Tooltip("弹幕销毁时距离左侧屏幕外的额外距离。")]
    private float leftDespawnPadding = 80f;

    [SerializeField, Min(0f), Tooltip("按文本宽度计算屏幕外距离时额外预留的宽度。")]
    private float widthPadding = 24f;

    [SerializeField, Tooltip("自动生成弹幕时随机抽取的文本。为空时使用预制体自身文本。")]
    private string[] bulletContents =
    {
        "这段演出可以",
        "前方高能",
        "笑死",
        "这个节奏对了",
        "别挡字幕",
        "这句太长会飘得更快一点"
    };

    [Header("Speed")]
    [SerializeField, Min(0f), Tooltip("短弹幕的基础移动速度，单位是 UI 坐标每秒。")]
    private float minMoveSpeed = 120f;

    [SerializeField, Min(0f), Tooltip("每个字符追加的速度；字数越多，弹幕越快。")]
    private float speedPerCharacter = 8f;

    [SerializeField, Min(0f), Tooltip("弹幕最大移动速度，避免超长文本飞得太快。")]
    private float maxMoveSpeed = 360f;

    [Header("Lifecycle")]
    [SerializeField, Tooltip("是否使用 unscaledDeltaTime，暂停游戏时 UI 弹幕仍可移动。")]
    private bool useUnscaledTime = true;

    [SerializeField, Tooltip("当前控制器禁用时是否清理已生成的弹幕。")]
    private bool clearBulletsOnDisable = true;

    private readonly List<BulletBugController> activeBullets = new List<BulletBugController>();
    private readonly List<BulletBugController> pooledInstances = new List<BulletBugController>();
    private readonly Queue<BulletBugController> availableBullets = new Queue<BulletBugController>();
    private RectTransform cachedRoot;
    private float spawnTimer;
    private bool isSpawning;
    private bool hasWarnedMissingPrefab;
    private bool hasWarnedMissingRoot;

    /// <summary>
    /// 当前屏幕上仍由控制器管理的弹幕数量。
    /// </summary>
    public int ActiveBulletCount => activeBullets.Count;

    /// <summary>
    /// 当前对象池中已经创建出的弹幕实例数量。
    /// </summary>
    public int PoolInstanceCount => pooledInstances.Count;

    /// <summary>
    /// 当前对象池中可复用的空闲弹幕数量。
    /// </summary>
    public int AvailableBulletCount => availableBullets.Count;

    /// <summary>
    /// 初始化屏幕根节点和自动生成状态。
    /// </summary>
    private void Awake()
    {
        ClampSettings();
        EnsureRoot();

        if (prewarmPoolOnAwake)
        {
            PrewarmPool();
        }
    }

    /// <summary>
    /// 组件启用时按配置启动自动弹幕。
    /// </summary>
    private void OnEnable()
    {
        ScheduleNextSpawn(true);
        isSpawning = playOnEnable;
    }

    /// <summary>
    /// 组件禁用时停止自动生成，并按配置清理已有弹幕。
    /// </summary>
    private void OnDisable()
    {
        isSpawning = false;

        if (clearBulletsOnDisable)
        {
            ClearAllBullets();
        }
    }

    /// <summary>
    /// 每帧推进自动生成计时，并清掉外部销毁后留下的空引用。
    /// </summary>
    private void Update()
    {
        TickSpawnTimer();
        RemoveDestroyedBullets();
    }

    /// <summary>
    /// 从外部开启自动弹幕生成。
    /// </summary>
    public void StartSpawning()
    {
        isSpawning = true;
        ScheduleNextSpawn(false);
    }

    /// <summary>
    /// 从外部停止自动弹幕生成，已有弹幕会继续飘完生命周期。
    /// </summary>
    public void StopSpawning()
    {
        isSpawning = false;
    }

    /// <summary>
    /// 立即生成一条指定内容的弹幕，并返回生成出的单条控制脚本。
    /// </summary>
    public BulletBugController SpawnBullet(string content)
    {
        if (!CanSpawnBullet())
        {
            return null;
        }

        RemoveDestroyedBullets();
        if (activeBullets.Count >= poolCapacity)
        {
            return null;
        }

        BulletBugController bullet = RentBullet();
        if (bullet == null)
        {
            return null;
        }

        bullet.gameObject.SetActive(true);
        string resolvedContent = ResolveBulletContent(content, bullet);

        // 先写入文本并刷新布局，再用实际宽度计算屏幕外生成和销毁位置。
        bullet.SetContent(resolvedContent);
        Canvas.ForceUpdateCanvases();

        float bulletWidth = bullet.GetVisualWidth() + widthPadding;
        Vector2 spawnPosition = CreateSpawnPosition(bulletWidth);
        float despawnX = -cachedRoot.rect.width * 0.5f - leftDespawnPadding - bulletWidth * 0.5f;
        float moveSpeed = CalculateMoveSpeed(resolvedContent);

        bullet.Play(spawnPosition, despawnX, moveSpeed, useUnscaledTime, HandleBulletFinished);
        activeBullets.Add(bullet);
        return bullet;
    }

    /// <summary>
    /// 立即生成一条随机内容的弹幕。
    /// </summary>
    public BulletBugController SpawnRandomBullet()
    {
        return SpawnBullet(GetRandomContent());
    }

    /// <summary>
    /// 清理当前控制器生成并仍在管理中的所有弹幕，实例回收到对象池等待复用。
    /// </summary>
    public void ClearAllBullets()
    {
        while (activeBullets.Count > 0)
        {
            RecycleBullet(activeBullets[activeBullets.Count - 1]);
        }
    }

    /// <summary>
    /// 按配置补齐对象池，提前创建可复用弹幕实例。
    /// </summary>
    public void PrewarmPool()
    {
        if (!CanSpawnBullet())
        {
            return;
        }

        RemoveDestroyedBullets();

        while (pooledInstances.Count < poolCapacity)
        {
            BulletBugController bullet = CreateBulletInstance();
            ReturnBulletToPool(bullet);
        }
    }

    /// <summary>
    /// 推进自动生成计时，计时结束后尝试生成随机弹幕。
    /// </summary>
    private void TickSpawnTimer()
    {
        if (!isSpawning)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        spawnTimer -= deltaTime;

        if (spawnTimer > 0f)
        {
            return;
        }

        if (activeBullets.Count < poolCapacity)
        {
            SpawnRandomBullet();
        }

        ScheduleNextSpawn(false);
    }

    /// <summary>
    /// 设置下一条自动弹幕的生成倒计时。
    /// </summary>
    private void ScheduleNextSpawn(bool spawnImmediately)
    {
        spawnTimer = spawnImmediately ? 0f : Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    /// <summary>
    /// 判断当前配置是否足够生成弹幕。
    /// </summary>
    private bool CanSpawnBullet()
    {
        EnsureRoot();

        if (bulletPrefab == null)
        {
            WarnOnce(ref hasWarnedMissingPrefab, $"{nameof(BulletScreenController)} needs a bullet prefab.");
            return false;
        }

        if (cachedRoot == null)
        {
            WarnOnce(ref hasWarnedMissingRoot, $"{nameof(BulletScreenController)} needs a RectTransform root.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 缓存用于计算屏幕范围的 UI 根节点。
    /// </summary>
    private void EnsureRoot()
    {
        if (cachedRoot != null)
        {
            return;
        }

        cachedRoot = bulletRoot != null ? bulletRoot : transform as RectTransform;
    }

    /// <summary>
    /// 从对象池取出一条弹幕；池未满时创建新实例，池满时返回 null。
    /// </summary>
    private BulletBugController RentBullet()
    {
        RemoveDestroyedBullets();

        while (availableBullets.Count > 0)
        {
            BulletBugController bullet = availableBullets.Dequeue();
            if (bullet != null)
            {
                return bullet;
            }
        }

        if (pooledInstances.Count >= poolCapacity)
        {
            return null;
        }

        return CreateBulletInstance();
    }

    /// <summary>
    /// 创建一条新的池内弹幕实例，并默认隐藏等待复用。
    /// </summary>
    private BulletBugController CreateBulletInstance()
    {
        GameObject instance = Instantiate(bulletPrefab, cachedRoot);
        BulletBugController controller = GetOrAddBulletController(instance);
        controller.ResetForPool();
        pooledInstances.Add(controller);
        return controller;
    }

    /// <summary>
    /// 取得或动态补上单条弹幕控制脚本。
    /// </summary>
    private static BulletBugController GetOrAddBulletController(GameObject instance)
    {
        BulletBugController controller = instance.GetComponent<BulletBugController>();
        if (controller == null)
        {
            controller = instance.AddComponent<BulletBugController>();
        }

        return controller;
    }

    /// <summary>
    /// 将结束生命周期的弹幕从在屏列表移回对象池。
    /// </summary>
    private void RecycleBullet(BulletBugController bullet)
    {
        activeBullets.Remove(bullet);
        ReturnBulletToPool(bullet);
    }

    /// <summary>
    /// 隐藏弹幕并放入可复用队列。
    /// </summary>
    private void ReturnBulletToPool(BulletBugController bullet)
    {
        if (bullet == null)
        {
            return;
        }

        bullet.ResetForPool();

        if (!pooledInstances.Contains(bullet))
        {
            if (pooledInstances.Count >= poolCapacity)
            {
                return;
            }

            pooledInstances.Add(bullet);
        }

        if (!availableBullets.Contains(bullet))
        {
            availableBullets.Enqueue(bullet);
        }
    }

    /// <summary>
    /// 按 Root 中心坐标生成右侧屏幕外位置，并按概率随机到上半区或下半区。
    /// </summary>
    private Vector2 CreateSpawnPosition(float bulletWidth)
    {
        float x = cachedRoot.rect.width * 0.5f + rightSpawnPadding + bulletWidth * 0.5f;
        bool shouldSpawnUpperHalf = Random.value < upperHalfSpawnChance;
        float y = shouldSpawnUpperHalf ? Random.Range(0f, rootUpDistance) : Random.Range(-rootDownDistance, 0f);
        return new Vector2(x, y);
    }

    /// <summary>
    /// 根据弹幕字数计算移动速度，长弹幕会更快。
    /// </summary>
    private float CalculateMoveSpeed(string content)
    {
        int characterCount = string.IsNullOrEmpty(content) ? 0 : content.Length;
        float speed = minMoveSpeed + characterCount * speedPerCharacter;
        return Mathf.Clamp(speed, minMoveSpeed, maxMoveSpeed);
    }

    /// <summary>
    /// 从配置文本中随机取一条弹幕内容。
    /// </summary>
    private string GetRandomContent()
    {
        if (bulletContents == null || bulletContents.Length == 0)
        {
            return string.Empty;
        }

        return bulletContents[Random.Range(0, bulletContents.Length)];
    }

    /// <summary>
    /// 空内容时回退到预制体自身已经配置好的 TextMeshProUGUI 文本。
    /// </summary>
    private static string ResolveBulletContent(string content, BulletBugController bullet)
    {
        if (!string.IsNullOrEmpty(content))
        {
            return content;
        }

        TextMeshProUGUI text = bullet != null ? bullet.ContentText : null;
        return text != null ? text.text : string.Empty;
    }

    /// <summary>
    /// 单条弹幕生命周期结束时，回收到对象池。
    /// </summary>
    private void HandleBulletFinished(BulletBugController bullet)
    {
        RecycleBullet(bullet);
    }

    /// <summary>
    /// 移除已经被外部销毁的弹幕引用。
    /// </summary>
    private void RemoveDestroyedBullets()
    {
        bool hasDestroyedPooledInstance = false;

        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            if (activeBullets[i] == null)
            {
                activeBullets.RemoveAt(i);
            }
        }

        for (int i = pooledInstances.Count - 1; i >= 0; i--)
        {
            if (pooledInstances[i] == null)
            {
                pooledInstances.RemoveAt(i);
                hasDestroyedPooledInstance = true;
            }
        }

        if (hasDestroyedPooledInstance)
        {
            RebuildAvailableQueue();
        }
    }

    /// <summary>
    /// 外部销毁池对象后，重建可复用队列，避免队列里残留空引用。
    /// </summary>
    private void RebuildAvailableQueue()
    {
        int count = availableBullets.Count;
        for (int i = 0; i < count; i++)
        {
            BulletBugController bullet = availableBullets.Dequeue();
            if (bullet != null && pooledInstances.Contains(bullet))
            {
                availableBullets.Enqueue(bullet);
            }
        }
    }

    /// <summary>
    /// Inspector 改值时限制参数合法范围。
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
    }

    /// <summary>
    /// 统一修正弹幕生成和移动参数，避免运行时出现非法区间。
    /// </summary>
    private void ClampSettings()
    {
        minSpawnInterval = Mathf.Max(0.05f, minSpawnInterval);
        maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);
        poolCapacity = Mathf.Max(1, poolCapacity);
        rootUpDistance = Mathf.Max(0f, rootUpDistance);
        rootDownDistance = Mathf.Max(0f, rootDownDistance);
        upperHalfSpawnChance = Mathf.Clamp01(upperHalfSpawnChance);
        rightSpawnPadding = Mathf.Max(0f, rightSpawnPadding);
        leftDespawnPadding = Mathf.Max(0f, leftDespawnPadding);
        widthPadding = Mathf.Max(0f, widthPadding);
        minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        speedPerCharacter = Mathf.Max(0f, speedPerCharacter);
        maxMoveSpeed = Mathf.Max(minMoveSpeed, maxMoveSpeed);
    }

    /// <summary>
    /// 同类配置缺失只警告一次，避免刷屏。
    /// </summary>
    private void WarnOnce(ref bool hasWarned, string message)
    {
        if (hasWarned)
        {
            return;
        }

        hasWarned = true;
        Debug.LogWarning(message, this);
    }
}
