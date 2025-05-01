# Unity Singleton Shorthand Generator

A Roslyn incremental source generator that creates static shorthand classes for Unity singletons.


```csharp
// Before
Camera cam = ObjectLocator.Instance.UICamera;
```

```csharp
// After
Camera cam = O.UICamera;
```

## Installation

Follow the instructions [here](https://docs.unity3d.com/6000.0/Documentation/Manual/roslyn-analyzers.html).

## Usage

### 1. Mark your singleton class

```csharp
// Specify the name of the shorthand class
[GenerateStaticShorthandClass("O")]
public class ObjectLocator : MonoBehaviour
{
    public static ObjectLocator Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Mark fields to include in the shorthand
    [StaticProp]
    public Camera UICamera;
    
    [StaticProp]
    public Transform PlayerSpawn;
}
```

### 2. Use the generated shorthand

```csharp
// Access singleton properties directly
transform.LookAt(O.UICamera.transform);
transform.position = O.PlayerSpawn.position;
```
