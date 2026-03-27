using UnityEngine;
using System.Collections;
public class CubePrefabScript: MonoBehaviour{
    public int i = 0;                           //声明整型变量i
    public int j = 0;                           //声明整型变量j
    public Rigidbody CubePrefab;                //声明刚体CubePrefab
    public float x = 0.0f;                      //初始化x、y、z坐标
    public float y = 4.0f;
    public float z = 0.0f;
    public int n = 4;                           //声明实例化游戏对象的行数
    public float k = 2.0f;
    int count = 0;                              //声明一个计数器
    public Rigidbody[] BP;                      //声明刚体数组
    void Start(){                               //声明Start方法
        BP = new Rigidbody[10];         //按n动态初始化刚体数组
        count = 0;                              //将计数器置0
        for (i = 1; i <= n; i++){               //对变量i进行循环
            for (j = 0; j < i; j++){            //对变量j进行循环，在自定义位置实例化10个正方体
                Rigidbody clone = (Rigidbody)Instantiate(CubePrefab,
                    new Vector3(x - 2.0f * k * i + 4.0f * j * k, 2.0f, z - 2.0f * 1.75f * k * i), CubePrefab.rotation);
                BP[count++] = clone;
            }
        }   //Instantiate(prefab, position, rotation, parentTransform);
    }
}
