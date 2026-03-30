using Spine;
using Spine.Unity;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AnimationState = Spine.AnimationState;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float Speed;
    public static List<ItemData> items = new List<ItemData>();

    private SkeletonAnimation skeletonAnim;
    private AnimationState animState;
    private Skeleton skeleton;
    private Rigidbody playerRigidbody;
    private bool isStartOver;
    private bool isMoving;
    private Vector3 movementInput;

    public GameObject inventoryPanel;
    private bool isInventoryActive = false;
    public ScrollRect scroolRect;
    private bool isShowItem = false;
    private static List<GameObject> itemSlots = new List<GameObject>();
    private EventTrigger et;
    private bool isHoverOnItem = false;
    private string OnHoverItemName;
    private string OnHoverItemInfo;
    public TextMeshProUGUI title;
    public TextMeshProUGUI info;
    public GameObject itemInfoPanel;
    public static PlayerController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        skeletonAnim = GetComponent<SkeletonAnimation>();
        animState = skeletonAnim.AnimationState;
        skeleton = skeletonAnim.Skeleton;

        playerRigidbody = GetComponent<Rigidbody>();
        if (playerRigidbody == null)
        {
            playerRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        playerRigidbody.useGravity = false;
        playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

        isStartOver = false;
        isMoving = false;
        movementInput = Vector3.zero;
        Speed = 5;
        animState.Complete += OnStartAnim;
    }

    private void Start()
    {
        PlayAnim("start", false);
    }

    private void Update()
    {
        if (isStartOver)
        {
            MoveInput();
            ControlInventory();
            if (isInventoryActive && !isShowItem)
            {
                ShowItem();
                isShowItem = true;
            }
            if (isHoverOnItem)
            {
                ShowItemInfo();
            }
            else
            {
                HideItemInfo();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isStartOver || playerRigidbody == null)
        {
            return;
        }

        if (isMoving)
        {
            Vector3 moveDirection = movementInput.normalized;
            playerRigidbody.linearVelocity = new Vector3(moveDirection.x * Speed, playerRigidbody.linearVelocity.y, moveDirection.z * Speed);
        }
        else
        {
            playerRigidbody.linearVelocity = new Vector3(0f, playerRigidbody.linearVelocity.y, 0f);
        }
    }

    private void OnStartAnim(TrackEntry track)
    {
        animState.Complete -= OnStartAnim;
        PlayAnim("idle", true);
        isStartOver = true;
    }

    private void MoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        isMoving = horizontal != 0 || vertical != 0;
        if (InteractDialog.isOnDialog || isInventoryActive)
        {
            isMoving = false;
        }

        movementInput = isMoving ? new Vector3(horizontal, 0f, vertical) : Vector3.zero;

        if (isMoving)
        {
            skeleton.ScaleX = horizontal > 0 ? 1 : -1;
            PlayAnim("walk", true);
        }
        else
        {
            PlayAnim("idle", true);
        }
    }

    private void PlayAnim(string AnimName, bool isLoop)
    {
        TrackEntry currentTrack = animState.GetCurrent(0);
        if (currentTrack != null && currentTrack.Animation.Name == AnimName) return;
        animState.SetAnimation(0, AnimName, isLoop);
    }

    private void ControlInventory()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && !isInventoryActive)
        {
            inventoryPanel.gameObject.SetActive(true);
            isInventoryActive = true;
        }
        else if (Input.GetKeyDown(KeyCode.Tab) && isInventoryActive)
        {
            inventoryPanel.gameObject.SetActive(false);
            isInventoryActive = false;
            isShowItem = false;
            for (int i = 0; i < itemSlots.Count; i++)
            {
                Destroy(itemSlots[i]);
            }
        }
    }

    private void ShowItem()
    {
        for (int i = 0; i < items.Count; i++)
        {
            GameObject item = Instantiate(Resources.Load<GameObject>("Prefabs/item"));
            ItemInfo itemData = item.AddComponent<ItemInfo>();
            itemData.name = items[i].name;
            itemData.describe = items[i].describe;
            AddIsHoverLisenerToItem(item);
            Image img = item.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>("Picture/" + items[i].name);
            item.transform.SetParent(scroolRect.content, false);
            item.transform.localPosition = new Vector3(10, -10, 0) + new Vector3(i * 250, 0, 0);
            scroolRect.content.sizeDelta = new Vector2((i + 1) * 250, 223);
            itemSlots.Add(item);
        }
    }

    private void AddIsHoverLisenerToItem(GameObject item)
    {
        et = item.GetComponent<EventTrigger>();
        et.triggers.Clear();
        EventTrigger.Entry entry1 = new EventTrigger.Entry();
        entry1.eventID = EventTriggerType.PointerEnter;
        entry1.callback.AddListener((data) =>
        {
            isHoverOnItem = true;
            ItemInfo itemInfo = item.GetComponent<ItemInfo>();
            OnHoverItemName = itemInfo.name;
            OnHoverItemInfo = itemInfo.describe;
        });
        EventTrigger.Entry entry2 = new EventTrigger.Entry();
        entry2.eventID = EventTriggerType.PointerExit;
        entry2.callback.AddListener((data) =>
        {
            isHoverOnItem = false;
        });
        et.triggers.Add(entry1);
        et.triggers.Add(entry2);
    }

    public void UpdateShowItem()
    {
        if (!isInventoryActive) return;
        for (int i = 0; i < itemSlots.Count; i++)
        {
            Destroy(itemSlots[i]);
        }
        ShowItem();
    }

    private void ShowItemInfo()
    {
        title.text = OnHoverItemName;
        info.text = OnHoverItemInfo;
        itemInfoPanel.gameObject.SetActive(true);
        itemInfoPanel.transform.position = Input.mousePosition;
    }

    private void HideItemInfo()
    {
        itemInfoPanel.gameObject.SetActive(false);
    }
}
