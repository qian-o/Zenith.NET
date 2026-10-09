# C# 代码规范

本规范约定 C# 代码的书写方式，涵盖格式、命名、类型设计、语言特性的用法以及常见场景的写法，适用于任何 C# 项目。项目结构、构建配置和依赖管理不在本规范范围内。

规则面向 .NET 10 与 C# 14。在较低版本中使用时，以等价写法替代不可用的特性（`field` 关键字、`extension` 块、`System.Threading.Lock` 等）。

用语约定：**必须**表示没有例外；**优先**表示默认做法，有明确理由时可以偏离；**避免**表示原则上不使用。修改既有代码时，与所在文件保持一致优先于本规范。

## 1. 基本原则

- **一致**：同一类问题只采用一种写法。
- **显式**：显式类型、显式访问修饰符、完整的大括号、直白的控制流；不依赖隐式推断，也不为少写几行而压缩逻辑。
- **自解释**：用命名、类型和结构表达意图，而不是依赖注释。
- **最小**：只实现当前需要的功能；不预留扩展点，不为一次性操作建立抽象，不在内部调用之间重复做防御式校验。
- **对称**：成对的操作成对命名、成对实现（`Create`/`Destroy`、`Begin`/`End`、`Acquire`/`Release`），释放顺序与创建顺序相反。
- **封装差异**：平台或实现之间的差异集中在实现类型内部处理，调用方不按平台或实现编写分支。
- **性能意识**：高频路径不产生托管分配；资源的所有权与生命周期清晰可追踪。

## 2. 文件组织

- 每个文件只包含一个主类型，文件名与主类型名相同（泛型类型不含类型参数）。以下情况例外：
  - 只服务于主类型的辅助类型（原生互操作声明、平台相关的封装、内部数据布局）与主类型写在同一文件中，放在主类型之后；被多个文件共用的类型才放到单独的文件中；
  - 按平台拆分的 `partial` 文件，命名为 `类型名.平台.cs`（如 `MainWindow.Windows.cs`），整个文件包在对应平台的 `#if` 中，只服务于该平台分支的辅助类型写在同一文件内；
  - 工具生成的 `partial` 文件，以 `.g.cs` 结尾，不手工修改。
- `global using` 集中声明在单独的文件中。

## 3. 格式

### 3.1 基本格式

| 项目 | 规则 |
| --- | --- |
| 编码 | UTF-8 with BOM |
| 文件结尾 | 以一个换行结束 |
| 缩进 | 4 个空格，不使用 Tab |
| 行尾空白 | 不允许 |
| 行宽 | 不设上限，不为凑行宽手动折行 |

### 3.2 大括号与特性

- 使用 Allman 风格，`{` 和 `}` 各占一行。
- `if`、`else`、`for`、`foreach`、`while` **必须**使用大括号，即使只有一条语句。
- 空方法体的 `{` 和 `}` 也分两行：

```csharp
protected override void OnClosed()
{
}
```

- 每个特性单独占一行；参数上的特性与参数写在同一行：

```csharp
[LibraryImport("libc", EntryPoint = "getenv")]
private static partial nint GetEnvironmentVariable([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
```

### 3.3 折行与对齐

- 每条语句写在一行内，包括较长的方法调用和条件。
- 参数确实过多时，每个参数单独一行，并与第一个参数对齐；不要只折行一部分。方法声明、主构造函数和方法调用都遵循这一规则：

```csharp
protected abstract void Initialize(bool useValidation,
                                   out Settings settings,
                                   out Connection primaryConnection,
                                   out Connection backupConnection,
                                   out ILogger? logger);
```

- 最后一个实参是对象初始化器、集合表达式、`switch` 表达式或 lambda 时，从同一行开始，内容按普通代码块缩进：

```csharp
BlobCache cache = new(new()
{
    CapacityInBytes = 64 * 1024 * 1024,
    Lifetime = TimeSpan.FromMinutes(10),
    Options = CacheOptions.SlidingExpiration
});
```

- 只有一个成员的对象初始化器可以写在一行：`new() { Count = 1 }`；有多个成员时每个成员占一行。
- 多个实参都是多行的代码块（对象初始化器、`switch` 表达式）时，`(` 和 `)` 各占一行，实参按普通代码块缩进，可配合命名实参：

```csharp
Shape shape = new
(
    circle: kind is ShapeKind.Circle ? new()
    {
        Center = center,
        Radius = radius
    } : null,
    rectangle: kind is ShapeKind.Rectangle ? new()
    {
        Origin = center,
        Size = size
    } : null
);
```

- 条件和三元表达式的条件部分不折行。条件确实过长时，`&&`、`||` 放在新行的行首。三元表达式的分支是对象初始化器时，初始化器可以按代码块展开（见上例）。

### 3.4 空行

空行用来分隔逻辑步骤：

1. using 块之后、命名空间声明之后各空一行；同一文件中的类型之间空一行。
2. 成员之间空一行，包括属性、方法、构造函数、事件、公共字段、结构体字段、接口成员和枚举成员。
3. 常量和私有字段按组紧凑排列：同一组（如全部 `private readonly` 字段）之间不空行，组与组之间空一行。
4. `{` 之后和 `}` 之前不留空行；任何位置都不出现连续两个空行。
5. 方法体内，每个逻辑步骤之间空一行。
6. `return` 之前空一行，除非它是代码块中的第一条语句；循环中的 `break`、`continue` 同理。`switch` 段末尾的 `break;` 直接跟在语句之后。
7. `}` 之后空一行，除非紧接 `else`、`catch`、`finally` 或另一个 `}`。唯一的例外：遍历集合、逐个处理元素（释放、提交、转发）后，紧接着清空同一集合；清空之后的下一步照常空一行。
8. 控制语句（`if`、`for`、`foreach`、`while`、`switch`）之前空一行；但上一行正好声明或初始化了该语句要使用的变量时，紧贴书写。
9. 紧密相关的语句之间不空行：对同一对象的连续操作、声明后立即作为 `out` 或指针实参传入的原生调用、成组的同类赋值或释放。

```csharp
public Order Submit(Cart cart)
{
    using Lock.Scope _ = @lock.EnterScope();

    Order order = new(++lastOrderId, [.. cart.Items]);

    pendingOrders.Enqueue(order);

    return order;
}
```

```csharp
decimal total = 0m;
foreach (OrderLine line in order.Lines)
{
    total += line.Price * line.Quantity;
}
```

```csharp
foreach (Connection connection in connections)
{
    connection.Dispose();
}
connections.Clear();

listener.Dispose();
```

```csharp
SystemInfo info;
GetSystemInfo(&info);

writer.WriteStartObject();
writer.WriteString("name", name);
writer.WriteEndObject();
```

## 4. using 与命名空间

- 使用文件范围命名空间：`namespace Contoso.Orders;`
- 命名空间与目录结构对应；仅用于归类文件的目录可以不体现在命名空间中。
- `using` 放在文件顶部、命名空间之外。`System.*` 在前，其余按字母序；别名放在最后；中间不空行、不分组：

```csharp
using System.Diagnostics;
using System.Text.Json;
using Contoso.Caching;
using Microsoft.Extensions.Logging;
using Timer = System.Threading.Timer;

namespace Contoso.Orders;
```

- 启用隐式 using 时，不重复引入已隐式导入的命名空间（`System`、`System.IO`、`System.Linq`、`System.Threading` 等）。
- 用别名解决名称冲突：
  - 单个文件内的冲突：`using Timer = System.Threading.Timer;`
  - 命名空间与类型同名、无法直接引用该类型时，为类型声明别名；
  - 整个项目中反复出现的冲突：用 `global using` 声明别名，加统一的来源前缀，并按字母序排列：

```csharp
global using DrawingColor = System.Drawing.Color;
global using DrawingPoint = System.Drawing.Point;
```

- 条件编译只用于区分目标平台（`#if ANDROID`、`#if WINDOWS`），不用于功能开关。运行时的平台判断使用 `OperatingSystem.IsWindows()` 等方法。

## 5. 命名

### 5.1 大小写

| 元素 | 规则 | 示例 |
| --- | --- | --- |
| 命名空间 | PascalCase | `Contoso.Caching` |
| 类、结构体、枚举、委托 | PascalCase | `BlobCache`、`CacheDesc` |
| 接口 | `I` + PascalCase | `IOrderRepository` |
| 泛型类型参数 | `T`，或 `T` + 描述 | `T`、`TResult` |
| 方法、局部函数 | PascalCase | `TryGet`、`ParseHeader` |
| 属性、事件 | PascalCase | `UsedBytes`、`Evicted` |
| 枚举成员 | PascalCase | `LogLevel.Warning` |
| 常量（含局部常量） | PascalCase | `MaxRetryCount` |
| 当作常量使用的 `static readonly` 字段 | PascalCase | `DefaultTimeout` |
| 非私有字段 | PascalCase | `Width`、`Handle` |
| 私有字段、保存状态的 `static readonly` 字段 | camelCase，无前缀 | `usedBytes`、`parsers` |
| 参数、局部变量 | camelCase | `cacheKey`、`sizeInBytes` |

- 私有字段不加 `_`、`m_`、`s_` 等前缀。构造函数参数与字段同名时用 `this.` 区分，其他地方不写 `this.`：

```csharp
public FileWatcher(string path, ILogger logger)
{
    this.logger = logger;

    watcher = new(path);
    watcher.Changed += OnChanged;
}
```

- 与 C# 关键字冲突时使用 `@` 前缀（`@lock`、`@event`），不改写成 `lockObject`、`_event` 之类的名称。
- `static readonly` 字段按用途区分：初始化后当作常量使用的值（`TimeSpan`、颜色、名称列表、预先计算的数据）用 PascalCase；保存对象或可变状态的字段（字典、缓存、单例服务）用 camelCase：

```csharp
private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
private static readonly Dictionary<string, IParser> parsers = [];
```

### 5.2 缩写

- 通用缩写按普通单词处理：`Api`、`Cpu`、`Gpu`、`Id`、`Url`（`ApiKey`、`GpuMemory`、`OrderId`）。
- 两个字母的缩写全部大写：`IO`、`UI`（`IOException`、`UIThread`）。
- 品牌名和带数字的规格名保持官方写法：`UTF8`、`SHA256`、`OpenGL`。
- 缩写位于 camelCase 名称开头时整体小写：`apiKey`、`uiThread`、`utf8Bytes`。
- 互操作代码中，与原生结构体或函数一一对应的成员可以保留原生拼写。
- 只使用领域内公认的缩写：`Desc`、`Args`、`Impl`、`Info`、`Src`、`Dst`；其余使用完整单词（`Configuration` 而不是 `Cfg`，`Message` 而不是 `Msg`）。

### 5.3 词汇与后缀

- 名称中带上单位：`SizeInBytes`、`OffsetInBytes`、`ElapsedSeconds`、`GetElapsedNanoseconds`。类型本身已经表达单位时（如 `TimeSpan`）不再添加。
- 数量用 `Count`（`ItemCount`），序号用 `Index`（`PageIndex`），起点用 `First` 或 `Base`（`FirstLine`、`BaseAddress`）。
- 源与目标成对出现时使用 `src`、`dst` 前缀，源在前：`CopyRange(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)`。
- 同一概念在整个代码库中使用同一个名称，例如参数统一叫 `desc`、`args`、`cancellationToken`，对应的属性叫 `Desc`。
- 类型后缀：

| 后缀 | 用途 | 示例 |
| --- | --- | --- |
| `Desc` | 创建对象所需的描述 | `CacheDesc` |
| `Args` | 每次调用的参数 | `ImportArgs` |
| `EventArgs` | 事件数据 | `CacheEvictedEventArgs` |
| `Helper` | 无状态的静态工具 | `PathHelper` |
| `Extensions` | 扩展成员的容器 | `Extensions` |

- 同一抽象的多个实现，以实现方式作为前缀区分（`FileLogStore`、`MemoryLogStore`）；转换为具体类型的局部变量沿用该前缀：`SqlCommand sqlCommand = (SqlCommand)command;`
- 循环索引使用 `i`、`j`、`k`；不使用的 lambda 参数、`out` 参数和解构元素写作 `_`。

### 5.4 布尔

| 场景 | 形式 | 示例 |
| --- | --- | --- |
| 状态 | `Is…`、`Has…`、`Can…`、`Owns…` | `IsDisposed`、`HasPendingWrites`、`CanSeek`、`OwnsStream` |
| 描述中的功能开关 | `Is…Enabled`，或直接 `Is…` | `IsCompressionEnabled`、`IsReadOnly` |
| 选择某种用法 | `Use…` | `UseProxy`、`useCache` |
| 能力查询 | `…Supported` | `CompressionSupported` |
| `Args` 中的单次标志 | 名词或形容词，不加 `Is` | `Force`、`Reset` |

避免否定形式的布尔名（`IsNotReady`、`DisableCache`）。

### 5.5 方法

- 以动词或动词短语命名。成对的操作使用对称的动词：`Create`/`Destroy`、`Begin`/`End`、`Open`/`Close`、`Acquire`/`Release`、`Map`/`Unmap`、`AddReference`/`RemoveReference`。
- 约定的模式：

| 模式 | 含义 |
| --- | --- |
| `CreateXxx(XxxDesc desc)` | 创建对象，调用方负责释放 |
| `TryXxx(..., out T value)` | 可能失败的操作，返回 `bool` |
| `XxxImpl(...)` | 由派生类实现的受保护钩子（见 6.5） |
| `OnXxx(...)` | 引发事件，或作为事件处理器（`OnMouseDown`） |
| `XxxAsync(...)` | 返回 `Task` 的异步方法 |

- 同一类转换使用同名方法，以重载区分源类型，方法名表示转换目标：`Mapper.ToDto(User user)`、`Mapper.ToDto(Order order)`。

### 5.6 枚举

- 普通枚举使用单数名词（`LogLevel`、`ConnectionState`）；标志枚举使用复数名词或 `Flags` 后缀（`Permissions`、`FileAttributes`、`BindingFlags`）。
- 标志枚举**必须**标记 `[Flags]`，包含 `None = 0`，其余值写成 `1 << n`。
- 成员按语义顺序排列（例如日志级别从低到高），不按字母序；公开后不随意调整顺序。
- 只有当值需要与外部（原生 API、协议、持久化数据）对应时才写显式值。

```csharp
[Flags]
public enum Permissions
{
    None = 0,

    Read = 1 << 0,

    Write = 1 << 1,

    Delete = 1 << 2
}
```

## 6. 类型设计

### 6.1 选择类型种类

| 场景 | 选择 | 示例 |
| --- | --- | --- |
| 持有非托管资源或有明确的生命周期 | `class`，继承 `DisposableObject` 并重写 `Destroy()`（见第 13 节） | `Connection`、`BlobCache` |
| 创建描述、调用参数 | 可变 `struct`，公共字段 | `CacheDesc`、`ImportArgs` |
| 小型不可变值 | `readonly struct` + 主构造函数 + `public readonly` 字段 | `Money` |
| 只在某个类型内部使用的值组合 | 嵌套的 `private readonly struct` | `BlobCache.Entry` |
| 与原生代码共享内存布局 | `struct` + `[StructLayout]`、`[FieldOffset]` | `PacketHeader` |
| 事件数据 | `class : EventArgs`，主构造函数 + 只读自动属性 | `CacheEvictedEventArgs` |
| 以 `in` 传递的大型只读参数包 | `readonly struct` + `{ get; init; }` 属性 | `RequestContext` |

不使用 `record` 和 `sealed`。

```csharp
public readonly struct Money(decimal amount, string currency)
{
    public readonly decimal Amount = amount;

    public readonly string Currency = currency;

    public bool IsZero => Amount is 0m;
}
```

### 6.2 描述与参数结构体

- 创建对象时传入 `XxxDesc`，对象以只读属性 `Desc` 保存它；每次调用时传入 `XxxArgs`：`new BlobCache(desc)`、`importer.Import(args)`。
- `Desc` 的字段顺序：种类或格式 → 尺寸和数量 → 模式和选项 → 布尔开关。
- `Args` 的字段顺序：输入 → 输出 → 数值参数 → 布尔标志。
- 常用配置以静态工厂方法提供，不使用静态属性；方法返回完整填充的新值：

```csharp
public static RetryDesc Exponential(int maxAttempts)
{
    return new()
    {
        MaxAttempts = maxAttempts,
        InitialDelay = TimeSpan.FromMilliseconds(200),
        BackoffFactor = 2.0,
        IsJitterEnabled = true
    };
}
```

- 在预设的基础上调整时使用 `with`：`CacheDesc desc = CacheDesc.Default() with { Lifetime = TimeSpan.FromMinutes(10) };`
- 用 `default` 或 `new()` 表示全部取默认值的结构体参数：`store.Write(key, data, default);`

### 6.3 构造函数

- 构造过程只做依赖传入时，使用主构造函数。在成员中直接使用参数，不再声明同名字段；需要对外公开时初始化为只读属性：

```csharp
public class OrderQueue(OrderQueueKind kind, int capacity)
{
    private readonly Queue<Order> orders = new(capacity);

    public OrderQueueKind Kind { get; } = kind;
}
```

- 参数在之后会被修改时，显式捕获为字段：`private CacheDesc desc = desc;`
- 构造过程包含多步初始化，或需要多个重载时，使用普通构造函数。
- 主构造函数的参数过多时，每个参数一行并与第一个参数对齐（见 3.3）。

### 6.4 可见性与修饰符

- 除接口成员与枚举成员外，**必须**显式写出访问修饰符。
- 采用最小可见性：实现类型默认为 `internal`，只有对外 API 才是 `public`。实现类型通过工厂方法或扩展成员以基类型暴露：

```csharp
extension(Storage)
{
    public static Storage CreateFileStorage(string rootPath)
    {
        return new FileStorage(rootPath);
    }
}
```

- 修饰符顺序：访问修饰符 → `static` → `new`、`virtual`、`abstract`、`override` → `readonly` → `unsafe` → `partial`。例如 `private static readonly`、`internal static unsafe class`、`internal readonly unsafe struct`、`internal unsafe partial class`。

### 6.5 抽象基类与实现

- 抽象基类提供非虚的公共方法，负责通用逻辑（状态维护、缓存、前置检查），再调用 `protected abstract` 的 `XxxImpl` 钩子：

```csharp
public void Write(ReadOnlySpan<byte> data)
{
    WriteImpl(data);

    bytesWritten += data.Length;
}

protected abstract void WriteImpl(ReadOnlySpan<byte> data);
```

- 公共方法与钩子一一对应，声明顺序一致；派生类中重写成员的顺序与基类的声明顺序一致。
- 派生类需要以具体类型访问基类成员时，用 `new` 隐藏：`public new FileStorage Storage => (FileStorage)base.Storage;`
- 某个实现不需要的钩子保留空方法体或返回默认值，不抛出 `NotImplementedException`。

### 6.6 接口、嵌套类型与 file 类型

- 接口成员不写访问修饰符，成员之间空一行。
- 不希望出现在类型公共表面上的接口成员使用显式实现：`void IObserver<Order>.OnCompleted()`。
- 只供包含类型使用的类型，声明为 `private` 嵌套类型，放在包含类型的末尾。
- 只服务于主类型的辅助类型放在主类型之后（见第 2 节）：数据结构（原生结构体、内部数据布局）声明为 `file` 类型；辅助类（原生互操作声明、平台相关的封装）声明为 `internal`，`[LibraryImport]`、`[GeneratedComInterface]` 等源生成器无法补全 `file` 类型。

### 6.7 partial 与扩展成员

- `partial` 只用于三种情况：源生成器（`[LibraryImport]`、`[GeneratedComInterface]` 等）、生成代码（`.g.cs`）、平台拆分文件。不用 `partial` 拆分手写的大型类型。
- 扩展成员使用 C# 14 的 `extension` 块，集中写在 `public static class Extensions` 中，每个接收者类型一个块。适用于静态工厂、为既有类型增加功能和内部便捷方法；内部便捷方法标记为 `internal`。

## 7. 成员顺序

类型中的成员按以下顺序排列：

1. 常量
2. `[LibraryImport]` 等源生成的原生方法声明
3. 静态字段（`static readonly` 在前）
4. 实例只读字段
5. 公共字段
6. 实例可变字段
7. 静态构造函数、构造函数、终结器
8. 属性（静态属性在前）
9. 事件
10. 实例方法：公共 → 内部 → 受保护 → 显式接口实现 → 私有
11. 静态方法：同样按 公共 → 内部 → 受保护 → 私有 排列，全部位于实例方法之后
12. 运算符
13. 嵌套类型（总是放在类型末尾）

补充规则：

- 辅助类型（包括 `file` 类型）放在主类型之后。
- 抽象基类中的 `protected abstract` 钩子集中放在一起，位于公共方法之后，顺序与对应的公共方法一致。
- 对象初始化器中成员的顺序与类型中的声明顺序一致。

```csharp
public class ConnectionPool : DisposableObject
{
    private const int DefaultCapacity = 16;

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    private readonly Lock @lock = new();
    private readonly Stack<Connection> idle = [];

    private int leased;

    public ConnectionPool(ConnectionPoolDesc desc)
    {
        ...
    }

    public ConnectionPoolDesc Desc { get; }

    public int Leased => leased;

    public event EventHandler<ConnectionEventArgs>? Created;

    public Connection Rent()
    {
        ...
    }

    public void Return(Connection connection)
    {
        ...
    }

    protected override void Destroy()
    {
        ...
    }

    private Connection Create()
    {
        ...
    }

    private readonly struct Lease(Connection connection, long timestamp)
    {
        ...
    }
}
```

## 8. 字段与属性

- 字段能声明为 `readonly` 就声明为 `readonly`。
- 集合字段用集合表达式初始化（`private readonly List<Order> orders = [];`），对象字段用 `new()` 初始化。
- 内部类型可以直接公开字段，不为其编写只做转发的属性。
- 属性的写法：

| 需求 | 写法 |
| --- | --- |
| 只读 | `public string Name { get; }` |
| 由其他状态计算（单行） | `public bool IsEmpty => count is 0;` |
| 外部只读、内部可写 | `public int Count { get; private set; }` |
| 只读结构体的初始化 | `public string Path { get; init; }` |
| 延迟创建 | `public Parser Parser => field ??= new(options);` |
| setter 中的变更检测 | 块体，使用 `field` 关键字 |

```csharp
public string Title
{
    get;
    set
    {
        if (field != value)
        {
            field = value;

            OnTitleChanged();
        }
    }
} = string.Empty;
```

- 体积较大的结构体通过 `ref readonly` 属性暴露，避免复制：`public ref readonly CacheDesc Desc => ref desc;`；作为参数时以 `in` 传递。

## 9. 方法与参数

- 方法总是使用块体，不使用表达式体（`=>`）。表达式体只用于单行的属性、索引器和访问器，以及 lambda。
- 用卫语句提前返回，避免深层嵌套：

```csharp
public void Send(Message message)
{
    if (!TryGetConnection(out Connection connection))
    {
        return;
    }

    connection.Write(message.Payload);
}
```

- 参数顺序：执行上下文或目标对象在前（如 `writer`、`connection`），然后是输入，最后是输出；成对参数源在前、目标在后。
- 多个返回值优先使用命名元组：`(int Quotient, int Remainder) Divide(int dividend, int divisor)`。`out` 参数用于 `Try` 模式、需要返回多个结果的初始化钩子，以及与原生 API 对应的签名。
- 可选参数只用来提供常用的默认值，主要出现在面向调用者的 API 中：`Load(string path, bool useCache = true)`。
- 命名实参只在字面量含义不明确时使用，主要是布尔值：`CreateClient(useProxy: true)`。
- 接收一组只读值时使用 `ReadOnlySpan<T>` 而不是数组，可变数量的参数使用 `params ReadOnlySpan<T>`；调用方以集合表达式传入：`Merge([first, second])`。
- 局部函数优先放在方法体末尾。

## 10. 语句与表达式

### 10.1 变量与对象创建

- **必须**写出显式类型，不使用 `var`；`out` 变量、`foreach` 变量和解构同样如此：

```csharp
foreach ((string key, Entry entry) in entries)
{
    ...
}

(int pageCount, int remainder) = Math.DivRem(itemCount, pageSize);

if (!int.TryParse(text, out int port))
{
    ...
}
```

- 每行只声明一个变量（`for` 的初始化子句除外）。
- 类型已知时使用目标类型 `new()`；要构造的类型与目标类型不同时写出类型名：`return new FileStorage(rootPath);`
- 集合使用集合表达式：`[]`、`[a, b]`、`[.. items]`。
- 使用关键字类型名（`string`、`int`、`nint`、`nuint`），不使用 `String`、`Int32`、`IntPtr`。空字符串写作 `string.Empty`。
- 数值字面量：`float` 写成 `1.0f`、`0.5f`（小写 `f`，保留小数部分）；`double` 写成 `1.0`；无符号整数用小写 `u`（`2u`），`ulong` 用 `UL`；十六进制使用大写字母（`0x1F`）；位标志写成 `1 << n`。

### 10.2 可空性与模式匹配

- 代码按启用可空引用类型编写：可能为 `null` 的引用必须标注 `?`，不使用 `#nullable disable`。
- 与常量比较时使用模式：`is null`、`is not null`、`is 0`、`is not 0`、`is LogLevel.Error`；变量之间的比较使用 `==`、`!=`。
- 多个候选值用 `or` 组合：`is LogLevel.Error or LogLevel.Critical`、`is not (ConnectionState.Open or ConnectionState.Connecting)`。
- 比较大小使用关系运算符，不使用关系模式：`count > 0`、`hresult < 0`。
- 类型检查与转换合并为声明模式：`if (stream is FileStream fileStream)`。类型确定时直接强制转换，不使用 `as`。
- 使用 `?.`、`??`、`??=` 处理可空值。
- null 宽恕运算符 `!` 只用于能够证明非空、但编译器无法推断的场合，例如在基类构造函数调用的 `Initialize()` 中赋值的字段：`private Parser parser = null!;`

### 10.3 分支

- 值的映射使用 `switch` 表达式：每个分支一行，以 `_ => default` 兜底；多个值映射到同一结果时用 `or` 合并，组与组之间空一行：

```csharp
int sizeInBytes = elementType switch
{
    ElementType.Int8 or
    ElementType.UInt8 => 1,

    ElementType.Int16 or
    ElementType.UInt16 or
    ElementType.Float16 => 2,

    ElementType.Int32 or
    ElementType.UInt32 or
    ElementType.Float32 => 4,

    _ => 0
};
```

- 分支需要执行多条语句时使用 `switch` 语句：`case` 缩进一级，`case` 段之间空一行，以 `break;` 结束；段内需要局部变量时用大括号包住，`break;` 写在 `}` 之后：

```csharp
switch (message.Kind)
{
    case MessageKind.Text:
        {
            string text = Encoding.UTF8.GetString(message.Payload);

            OnText(text);
        }
        break;

    case MessageKind.Close:
        Close();
        break;
}
```

- 三元表达式只用于简单的二选一；不嵌套三元表达式。除分支为对象初始化器的情况（见 3.3）外，写在一行内。多路选择使用 `switch` 表达式或 `if`/`else if`。
- 混合优先级的运算加括号表明意图：`offset + (rowStride * row)`、`(flags & mask) is not 0`。

### 10.4 Lambda 与 LINQ

- Lambda 优先使用表达式体；不捕获外部状态的 lambda 标记为 `static`：`items.RemoveAll(static item => item.IsExpired);`
- 单个参数的 lambda 不加括号：`item => item.Id`，而不是 `(item) => item.Id`。
- 不使用的 lambda 参数写作 `_`：`saveButton.Click += async (_, _) => await SaveAsync();`
- LINQ 使用方法语法，不使用查询语法；高频路径不使用 LINQ。

### 10.5 其他

- 资源使用 using 声明（`using FileStream stream = File.OpenRead(path);`），不使用 `using (...) { }` 块。只为限定作用域而持有的对象命名为 `_`：`using Lock.Scope _ = @lock.EnterScope();`
- 字符串拼接使用插值 `$"..."`；字符串比较显式指定 `StringComparison.Ordinal`；引用成员名称时使用 `nameof`。
- 基于既有值构造修改后的副本时使用 `with` 表达式。
- 不使用 `#region`；不在代码中用 `#pragma warning disable` 抑制警告，确需抑制时在配置中针对具体规则处理。生成代码除外。
- 代码中的标识符、字符串、注释、异常消息和日志统一使用英文。

## 11. 注释

- 默认不写注释，依靠命名和结构表达意图。不保留被注释掉的代码，不提交 `TODO`。
- 只在代码本身无法说明“为什么”时写注释：一行，完整的英文句子，以句号结尾：

```csharp
public void SetCompositionText(string text)
{
    // IME composition is not supported.
}
```

- 篇幅较长的初始化方法可以用作用域块分段，并在块前写一个简短的标签：

```csharp
// Headers
{
    ...
}

// Body
{
    ...
}
```

- 不编写 XML 文档注释：API 依靠命名和签名表达含义，概念性说明写在独立文档中。确实需要 XML 文档时，只写简短的 `<summary>`。
- 移植自第三方的代码，在类型上注明来源链接。
- 生成的文件以 `// <auto-generated/>` 开头或使用 `.g.cs` 后缀。

## 12. 错误处理

- 只在无法继续运行时抛出异常，例如运行环境不满足最低要求。异常消息是完整的英文句子：

```csharp
throw new PlatformNotSupportedException("This application only supports Windows, macOS, and Linux.");
```

- 只在系统边界（用户输入、文件、网络和其他外部数据）校验参数；不在内部调用之间重复做防御式校验，也不为不会发生的情况编写处理代码。
- 调用条件不满足、但不影响后续运行时，提前返回而不是抛出异常（见第 9 节）。
- 原生调用的返回码集中处理：定义 `Success()` 扩展方法，失败时通过 `Debug.WriteLine` 记录；需要据此分支时，另外定义返回 `bool` 的 `IsSuccess()`：

```csharp
extension(int hresult)
{
    internal void Success()
    {
        if (hresult < 0)
        {
            Debug.WriteLine($"Native call failed with HRESULT 0x{hresult:X8}.");
        }
    }
}
```

- 只捕获具体的异常类型；必须吞掉异常时，用注释说明原因。例外：在程序边界（工具、探测程序的入口）把任意失败转换为输出时，可以捕获 `Exception`。用 `try`/`finally` 保证信号、计数等收尾操作一定执行。

## 13. 资源与生命周期

- 需要释放的类型继承统一的 `DisposableObject` 基类并重写 `Destroy()`。基类保证 `Dispose()` 线程安全、只执行一次，并以终结器兜底：

```csharp
public abstract class DisposableObject : IDisposable
{
    private volatile uint isDisposed;

    ~DisposableObject()
    {
        Dispose();
    }

    public bool IsDisposed => isDisposed is not 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
        {
            return;
        }

        Destroy();

        GC.SuppressFinalize(this);
    }

    protected abstract void Destroy();
}
```

- 构造函数中途失败时，终结器仍会调用 `Destroy()`，因此 `Destroy()` 需要能够处理未完成构造的对象（检查初始化标志或可空成员）。
- 谁创建谁释放；`CreateXxx` 返回的对象由调用方释放。
- 按与创建相反的顺序释放：

```csharp
protected override void Destroy()
{
    writer.Dispose();
    stream.Dispose();
    connection.Dispose();
}
```

- 可空成员用 `?.Dispose()` 释放；之后需要重新创建的成员，释放后立即置为 `null`：

```csharp
timer?.Dispose();
timer = null;
```

- 需要重建的资源编写成对的私有方法 `CreateXxx()`、`DestroyXxx()`，供构造、重建和释放共同使用。
- 多个使用者共享的对象用引用计数管理（`AddReference()`、`RemoveReference()`），计数归零时从持有它的集合中移除并释放。

## 14. 并发与异步

- 使用 `System.Threading.Lock` 的作用域写法加锁，不使用 `lock` 语句或 `Monitor`：

```csharp
private readonly Lock @lock = new();
private readonly Stack<int> freeIds = [];

public void Release(int id)
{
    using Lock.Scope _ = @lock.EnterScope();

    freeIds.Push(id);
}
```

- 简单的原子状态使用 `Interlocked` 和 `volatile` 字段。
- 专用线程设置 `Name` 和 `IsBackground`；线程之间传递任务使用 `BlockingCollection<T>` 等线程安全集合。
- 异步方法返回 `Task`，以 `Async` 结尾；事件处理器中写作 `async (_, _) => await ...`；后台循环通过 `CancellationTokenSource` 停止。
- 每个线程独立的上下文使用 `[ThreadStatic]` 静态字段。

## 15. 性能、unsafe 与互操作

- 高频路径避免托管分配：使用 `stackalloc`、`Span<T>`、`ArrayPool<T>.Shared` 和预先分配的缓存数组。
- 大量使用指针的类型在类型声明上标记 `unsafe`；只在个别位置需要指针时使用 `unsafe { }` 块。
- 原生指针变量可以沿用原生 API 的 `p`、`pp` 前缀（`pBuffer`、`ppNames`）。
- 调用动态获取的原生函数时，使用函数指针 `delegate* unmanaged<...>`。
- 非托管内存由负责统一释放的作用域对象分配，或用 `NativeMemory.Alloc`、`NativeMemory.Free` 成对管理。
- 传给原生代码的委托必须由字段持有（必要时配合 `GCHandle`），确保在原生代码使用期间不被回收。
- 使用 `[LibraryImport]` 和 `[GeneratedComInterface]` 生成互操作代码，不使用 `[DllImport]`、`[ComImport]`，保持 AOT 兼容。
- 重载解析需要时，用 `default(T*)` 传递空指针：`default(SecurityAttributes*)`。
- 与原生代码共享的结构体显式声明布局；需要精确偏移时使用 `LayoutKind.Explicit` 并写明 `Size`：

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct PacketHeader
{
    [FieldOffset(0)]
    public uint Magic;

    [FieldOffset(4)]
    public ushort Version;

    [FieldOffset(8)]
    public ulong PayloadSizeInBytes;
}
```

## 附录 A：.editorconfig

将以下配置放在仓库根目录的 `.editorconfig` 中。项目启用 `EnforceCodeStyleInBuild` 后，严重性为 `warning` 的规则会在构建时报告；`IDE0003`、`IDE0049` 等少数规则只在 IDE 中提示。`static readonly` 字段的命名取决于用途（见 5.1），无法用命名规则表达，因此该组的严重性设为 `none`。

```ini
root = true

[*.cs]
charset = utf-8-bom
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

# Namespaces and usings
csharp_style_namespace_declarations = file_scoped
csharp_using_directive_placement = outside_namespace
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
dotnet_style_namespace_match_folder = true

# Types and qualification
csharp_style_var_for_built_in_types = false
csharp_style_var_when_type_is_apparent = false
csharp_style_var_elsewhere = false
dotnet_style_predefined_type_for_locals_parameters_members = true
dotnet_style_predefined_type_for_member_access = true
dotnet_style_qualification_for_field = false
dotnet_style_qualification_for_property = false
dotnet_style_qualification_for_method = false
dotnet_style_qualification_for_event = false

# Modifiers
dotnet_style_require_accessibility_modifiers = for_non_interface_members
csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async
dotnet_style_readonly_field = true

# Expression bodies
csharp_style_expression_bodied_methods = false
csharp_style_expression_bodied_constructors = false
csharp_style_expression_bodied_operators = false
csharp_style_expression_bodied_local_functions = false
csharp_style_expression_bodied_properties = when_on_single_line
csharp_style_expression_bodied_indexers = when_on_single_line
csharp_style_expression_bodied_accessors = when_on_single_line
csharp_style_expression_bodied_lambdas = true

# Language features
csharp_prefer_braces = true
csharp_prefer_simple_using_statement = true
csharp_style_implicit_object_creation_when_type_is_apparent = true
dotnet_style_prefer_collection_expression = when_types_loosely_match
csharp_style_prefer_primary_constructors = true
csharp_style_prefer_switch_expression = true
csharp_style_prefer_pattern_matching = true
csharp_style_prefer_not_pattern = true
csharp_style_pattern_matching_over_is_with_cast_check = true
csharp_style_pattern_matching_over_as_with_null_check = true
csharp_style_prefer_null_check_over_type_check = true
dotnet_style_prefer_is_null_check_over_reference_equality_method = true
dotnet_style_coalesce_expression = true
dotnet_style_null_propagation = true
dotnet_style_prefer_conditional_expression_over_assignment = false
dotnet_style_prefer_conditional_expression_over_return = false
csharp_style_inlined_variable_declaration = true
csharp_prefer_static_anonymous_functions = true
csharp_prefer_system_threading_lock = true
dotnet_style_parentheses_in_arithmetic_binary_operators = always_for_clarity
dotnet_style_parentheses_in_relational_binary_operators = always_for_clarity
dotnet_style_parentheses_in_other_binary_operators = always_for_clarity
dotnet_style_parentheses_in_other_operators = never_if_unnecessary

# Formatting
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_indent_case_contents_when_block = true
csharp_preserve_single_line_blocks = true
csharp_preserve_single_line_statements = false

# Naming styles
dotnet_naming_style.pascal_case.capitalization = pascal_case
dotnet_naming_style.camel_case.capitalization = camel_case
dotnet_naming_style.prefix_i.capitalization = pascal_case
dotnet_naming_style.prefix_i.required_prefix = I
dotnet_naming_style.prefix_t.capitalization = pascal_case
dotnet_naming_style.prefix_t.required_prefix = T

# Naming symbols
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter
dotnet_naming_symbols.types_and_members.applicable_kinds = namespace, class, struct, enum, delegate, method, property, event, local_function
dotnet_naming_symbols.private_constants.applicable_kinds = field
dotnet_naming_symbols.private_constants.applicable_accessibilities = private
dotnet_naming_symbols.private_constants.required_modifiers = const
dotnet_naming_symbols.local_constants.applicable_kinds = local
dotnet_naming_symbols.local_constants.required_modifiers = const
dotnet_naming_symbols.private_static_readonly_fields.applicable_kinds = field
dotnet_naming_symbols.private_static_readonly_fields.applicable_accessibilities = private
dotnet_naming_symbols.private_static_readonly_fields.required_modifiers = static, readonly
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_symbols.non_private_fields.applicable_kinds = field
dotnet_naming_symbols.non_private_fields.applicable_accessibilities = public, internal, protected, protected_internal, private_protected
dotnet_naming_symbols.parameters_and_locals.applicable_kinds = parameter, local

# Naming rules
dotnet_naming_rule.interfaces.symbols = interfaces
dotnet_naming_rule.interfaces.style = prefix_i
dotnet_naming_rule.interfaces.severity = warning
dotnet_naming_rule.type_parameters.symbols = type_parameters
dotnet_naming_rule.type_parameters.style = prefix_t
dotnet_naming_rule.type_parameters.severity = warning
dotnet_naming_rule.types_and_members.symbols = types_and_members
dotnet_naming_rule.types_and_members.style = pascal_case
dotnet_naming_rule.types_and_members.severity = warning
dotnet_naming_rule.private_constants.symbols = private_constants
dotnet_naming_rule.private_constants.style = pascal_case
dotnet_naming_rule.private_constants.severity = warning
dotnet_naming_rule.local_constants.symbols = local_constants
dotnet_naming_rule.local_constants.style = pascal_case
dotnet_naming_rule.local_constants.severity = warning
dotnet_naming_rule.private_static_readonly_fields.symbols = private_static_readonly_fields
dotnet_naming_rule.private_static_readonly_fields.style = pascal_case
dotnet_naming_rule.private_static_readonly_fields.severity = none
dotnet_naming_rule.private_fields.symbols = private_fields
dotnet_naming_rule.private_fields.style = camel_case
dotnet_naming_rule.private_fields.severity = warning
dotnet_naming_rule.non_private_fields.symbols = non_private_fields
dotnet_naming_rule.non_private_fields.style = pascal_case
dotnet_naming_rule.non_private_fields.severity = warning
dotnet_naming_rule.parameters_and_locals.symbols = parameters_and_locals
dotnet_naming_rule.parameters_and_locals.style = camel_case
dotnet_naming_rule.parameters_and_locals.severity = warning

# Severities
dotnet_diagnostic.IDE0003.severity = warning
dotnet_diagnostic.IDE0008.severity = warning
dotnet_diagnostic.IDE0011.severity = warning
dotnet_diagnostic.IDE0018.severity = warning
dotnet_diagnostic.IDE0019.severity = warning
dotnet_diagnostic.IDE0020.severity = warning
dotnet_diagnostic.IDE0021.severity = warning
dotnet_diagnostic.IDE0022.severity = warning
dotnet_diagnostic.IDE0023.severity = warning
dotnet_diagnostic.IDE0024.severity = warning
dotnet_diagnostic.IDE0025.severity = warning
dotnet_diagnostic.IDE0026.severity = warning
dotnet_diagnostic.IDE0027.severity = warning
dotnet_diagnostic.IDE0028.severity = warning
dotnet_diagnostic.IDE0029.severity = warning
dotnet_diagnostic.IDE0030.severity = warning
dotnet_diagnostic.IDE0031.severity = warning
dotnet_diagnostic.IDE0036.severity = warning
dotnet_diagnostic.IDE0038.severity = warning
dotnet_diagnostic.IDE0040.severity = warning
dotnet_diagnostic.IDE0041.severity = warning
dotnet_diagnostic.IDE0044.severity = warning
dotnet_diagnostic.IDE0047.severity = warning
dotnet_diagnostic.IDE0048.severity = warning
dotnet_diagnostic.IDE0049.severity = warning
dotnet_diagnostic.IDE0053.severity = warning
dotnet_diagnostic.IDE0055.severity = warning
dotnet_diagnostic.IDE0061.severity = warning
dotnet_diagnostic.IDE0063.severity = warning
dotnet_diagnostic.IDE0065.severity = warning
dotnet_diagnostic.IDE0066.severity = warning
dotnet_diagnostic.IDE0078.severity = warning
dotnet_diagnostic.IDE0083.severity = warning
dotnet_diagnostic.IDE0090.severity = warning
dotnet_diagnostic.IDE0130.severity = warning
dotnet_diagnostic.IDE0150.severity = warning
dotnet_diagnostic.IDE0161.severity = warning
dotnet_diagnostic.IDE0270.severity = warning
dotnet_diagnostic.IDE0290.severity = warning
dotnet_diagnostic.IDE0300.severity = warning
dotnet_diagnostic.IDE0301.severity = warning
dotnet_diagnostic.IDE0302.severity = warning
dotnet_diagnostic.IDE0303.severity = warning
dotnet_diagnostic.IDE0304.severity = warning
dotnet_diagnostic.IDE0305.severity = warning
dotnet_diagnostic.IDE0330.severity = warning
dotnet_diagnostic.IDE1006.severity = warning
```

## 附录 B：完整示例

下面的示例综合了本规范的主要规则，依赖第 13 节的 `DisposableObject`。

**CacheOptions.cs**

```csharp
namespace Contoso.Caching;

[Flags]
public enum CacheOptions
{
    None = 0,

    SlidingExpiration = 1 << 0,

    TrackStatistics = 1 << 1
}
```

**CacheDesc.cs**

```csharp
namespace Contoso.Caching;

public struct CacheDesc
{
    public uint CapacityInBytes;

    public TimeSpan Lifetime;

    public CacheOptions Options;

    public static CacheDesc Default()
    {
        return new()
        {
            CapacityInBytes = 64 * 1024 * 1024,
            Lifetime = TimeSpan.FromSeconds(120),
            Options = CacheOptions.SlidingExpiration
        };
    }
}
```

**CacheEvictedEventArgs.cs**

```csharp
namespace Contoso.Caching;

public class CacheEvictedEventArgs(string key, uint sizeInBytes) : EventArgs
{
    public string Key { get; } = key;

    public uint SizeInBytes { get; } = sizeInBytes;
}
```

**BlobCache.cs**

```csharp
using System.Diagnostics;

namespace Contoso.Caching;

public class BlobCache(CacheDesc desc) : DisposableObject
{
    private const uint MinCapacityInBytes = 4096;

    private readonly Dictionary<string, Entry> entries = [];

    private uint usedBytes;

    public CacheDesc Desc { get; } = desc;

    public uint UsedBytes => usedBytes;

    public event EventHandler<CacheEvictedEventArgs>? Evicted;

    public bool TryGet(string key, out byte[] data)
    {
        if (!entries.TryGetValue(key, out Entry entry) || entry.IsExpired)
        {
            data = [];

            return false;
        }

        if (Desc.Options.HasFlag(CacheOptions.SlidingExpiration))
        {
            entries[key] = entry.Renew(Desc.Lifetime);
        }

        data = entry.Data;

        return true;
    }

    public void Add(string key, byte[] data)
    {
        if (data.Length is 0)
        {
            return;
        }

        Remove(key);

        uint capacityInBytes = Math.Max(Desc.CapacityInBytes, MinCapacityInBytes);
        while (entries.Count is not 0 && usedBytes + (uint)data.Length > capacityInBytes)
        {
            Remove(entries.MinBy(static item => item.Value.ExpirationTimestamp).Key);
        }

        entries[key] = Entry.Create(data, Desc.Lifetime);
        usedBytes += (uint)data.Length;
    }

    protected override void Destroy()
    {
        entries.Clear();
        usedBytes = 0;
    }

    private void Remove(string key)
    {
        if (!entries.Remove(key, out Entry entry))
        {
            return;
        }

        usedBytes -= (uint)entry.Data.Length;

        Evicted?.Invoke(this, new(key, (uint)entry.Data.Length));
    }

    private readonly struct Entry(byte[] data, long expirationTimestamp)
    {
        public readonly byte[] Data = data;

        public readonly long ExpirationTimestamp = expirationTimestamp;

        public bool IsExpired => Stopwatch.GetTimestamp() >= ExpirationTimestamp;

        public Entry Renew(TimeSpan lifetime)
        {
            return Create(Data, lifetime);
        }

        public static Entry Create(byte[] data, TimeSpan lifetime)
        {
            return new(data, Stopwatch.GetTimestamp() + (long)(lifetime.TotalSeconds * Stopwatch.Frequency));
        }
    }
}
```

## 附录 C：评审清单

- [ ] 文件为 UTF-8 with BOM，以换行结束，4 空格缩进，没有行尾空白；一个文件只有一个主类型，其余类型只服务于它。
- [ ] 使用文件范围命名空间；`using` 顺序正确，没有多余的 `using`。
- [ ] 没有 `var`；成员都显式写出访问修饰符；能声明为 `readonly` 的字段都已声明。
- [ ] 命名符合第 5 节：私有字段没有前缀，常量使用 PascalCase，布尔、单位和缩写符合约定。
- [ ] 控制语句都有大括号；没有嵌套的三元表达式，三元表达式只在分支为对象初始化器时跨行。
- [ ] 空行符合 3.4：`return` 之前、`}` 之后、逻辑步骤之间。
- [ ] 成员顺序符合第 7 节；对象初始化器的成员顺序与声明顺序一致。
- [ ] 没有多余的注释、被注释掉的代码或 `TODO`。
- [ ] 只在无法继续时抛出异常；没有多余的参数校验。
- [ ] 资源由创建方按逆序释放；`Destroy()` 能处理未完成构造的对象。
- [ ] 高频路径没有托管分配和 LINQ。
- [ ] 没有编译器和分析器警告。
