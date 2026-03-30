using NUnit.Framework;
using Spine;
using Spine.Unity;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AnimationState = Spine.AnimationState;

/// <summary>
/// Spine角色启动动画+AD移动动画控制
/// 挂载在带有SkeletonAnimation的Spine角色物体上
/// </summary>
public class PlayerController : MonoBehaviour
{
    public float Speed;
    public static List<ItemData> items=new List<ItemData>();
    private SkeletonAnimation skeletonAnim;
    private AnimationState animState;
    private Skeleton skeleton;
    private bool isStartOver;
    private bool isMoving;
    public GameObject inventoryPanel;
    private bool isInventoryActive=false;
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
        skeletonAnim = this.GetComponent<SkeletonAnimation>();
        animState = skeletonAnim.AnimationState;
        skeleton = skeletonAnim.Skeleton;
        isStartOver = false;
        isMoving = false;
        Speed = 5;
        animState.Complete += OnStartAnim;
    }
    private void Start()
    {
        PlayAnim( "start", false);
    }
    private void Update()
    {
        if (isStartOver)
        {
            MoveInput();
            ControlInventory();
            if (isInventoryActive&&!isShowItem)
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
    private void OnStartAnim(TrackEntry track)
    {
        animState.Complete -= OnStartAnim;
        PlayAnim( "idle", true);
        isStartOver = true;
    }
    private void MoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        isMoving = horizontal != 0 || vertical != 0 ? true : false;
        if(InteractDialog.isOnDialog||isInventoryActive) isMoving = false;
        if (isMoving)
        {
            Vector3 movePos = new Vector3(horizontal * Speed * Time.deltaTime, 0, vertical * Speed * Time.deltaTime);
            this.transform.Translate(movePos);
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
        if (currentTrack.Animation.Name==AnimName) return;
        animState.SetAnimation(0, AnimName, isLoop);
    }
    private void ControlInventory()
    {
        if (Input.GetKeyDown(KeyCode.Tab)&&!isInventoryActive)
        {
            inventoryPanel.gameObject.SetActive(true);
            isInventoryActive = true;
        }
        else if (Input.GetKeyDown(KeyCode.Tab) && isInventoryActive)
        {
            inventoryPanel.gameObject.SetActive(false);
            isInventoryActive = false;
            isShowItem = false;
            for(int i = 0; i < itemSlots.Count; i++)
            {
                Destroy(itemSlots[i]);
            }
        }
    }
    private void ShowItem()
    {
        for(int i=0;i<items.Count;i++)
        {
            GameObject item=Instantiate(Resources.Load<GameObject>("Prefabs/item"));
            ItemInfo itemData=item.AddComponent<ItemInfo>();
            itemData.name = items[i].name;
            itemData.describe = items[i].describe;
            AddIsHoverLisenerToItem(item);
            Image img = item.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>("Picture/"+items[i].name);
            item.transform.SetParent(scroolRect.content,false);
            item.transform.localPosition = new Vector3(10, -10, 0) + new Vector3(i * 250, 0, 0);
            scroolRect.content.sizeDelta = new Vector2((i+1)*250, 223);
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
            OnHoverItemName=itemInfo.name;
            OnHoverItemInfo=itemInfo.describe;
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
