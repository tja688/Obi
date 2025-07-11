using System;
using UnityEngine;
using Obi;

// 建议将文件名也更改为 DualCraneController.cs 以匹配类名
namespace Obi.Samples 
{
    public class DualCraneController : MonoBehaviour
    {
        [Header("绳索设置 (Rope Settings)")]
        [Tooltip("左边的绳索光标 (Left Rope Cursor)")]
        public ObiRopeCursor leftCursor;

        [Tooltip("右边的绳索光标 (Right Rope Cursor)")]
        public ObiRopeCursor rightCursor;

        [Header("控制参数 (Control Parameters)")]
        [Tooltip("绳索收放速度 (Speed of rope length change)")]
        public float speed = 1f;

        [Tooltip("绳索允许的最小长度 (Minimum allowed rope length)")]
        public float minLength = 1f;

        [Tooltip("绳索允许的最大长度 (Maximum allowed rope length)")]
        public float maxLength = 20f;

        private void OnEnable()
        {
            PlayerControl_Ball.instance.playerControl = false;
        }
        
        private void OnDisable()
        {
            PlayerControl_Ball.instance.playerControl = true;
        }

        void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// 处理所有输入逻辑
        /// </summary>
        private void HandleInput()
        {
            // --- 双绳控制 ---
            // W: 同时收缩两根绳索
            if (Input.GetKey(KeyCode.W))
            {
                ChangeRopeLength(leftCursor, -1);
                ChangeRopeLength(rightCursor, -1);
            }

            // S: 同时伸长两根绳索
            if (Input.GetKey(KeyCode.S))
            {
                ChangeRopeLength(leftCursor, 1);
                ChangeRopeLength(rightCursor, 1);
            }

            // --- 单绳收缩 ---
            // Q: 收缩右绳
            if (Input.GetKey(KeyCode.Q))
            {
                ChangeRopeLength(rightCursor, -1);
            }

            // E: 收缩左绳
            if (Input.GetKey(KeyCode.E))
            {
                ChangeRopeLength(leftCursor, -1);
            }

            // --- 单绳伸长 ---
            // A: 伸长右绳
            if (Input.GetKey(KeyCode.A))
            {
                ChangeRopeLength(rightCursor, 1);
            }

            // D: 伸长左绳
            if (Input.GetKey(KeyCode.D))
            {
                ChangeRopeLength(leftCursor, 1);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// 改变指定绳索的长度
        /// </summary>
        /// <param name="cursor">要操作的绳索光标</param>
        /// <param name="direction">方向 (-1 为收缩, 1 为伸长)</param>
        private void ChangeRopeLength(ObiRopeCursor cursor, int direction)
        {
            // 确保 cursor 不为空
            if (cursor == null)
            {
                return;
            }

            var change = direction * speed * Time.deltaTime;
            
            var rope = cursor.GetComponent<ObiRope>();

            switch (direction)
            {
                // 检查是否超出最小/最大长度限制
                case < 0 when rope.restLength + change <= minLength:
                // 阻止伸长
                case > 0 when rope.restLength + change >= maxLength:
                    return; // 阻止收缩
                default:
                    cursor.ChangeLength(change);
                    break;
            }
        }
    }
}