# Redis / Sentinel testlerinin kullanımı

Bu .NET 5 test projesi doğrudan DMicroservices kaynak kodunu referans alır. Kontroller LEDMS'teki `ElasticMigration/CompatibilityProbe` içinden taşındı. Distributed lock kontrolleri paketin `RedLockManager` sınıfını kullanır.

## Docker ile çalıştırma

PowerShell 7 (`pwsh`), çalışan Docker Desktop ve bilgisayarınızda .NET SDK gerekir. Repository'nin ana klasöründe:

```powershell
dotnet restore Tests/DMicroservices.Redis.Compatibility
pwsh -File Tests/DMicroservices.Redis.Compatibility/Run-RedisCompatibility.ps1
```

Script projeyi Release olarak build eder, Docker Compose ile geçici test ortamını başlatır ve test uygulamasını Linux .NET 5 container'ında çalıştırır. Önce standalone Redis, ardından Sentinel failover senaryosu kontrol edilir.

Docker'da şu image'lar önceden bulunmalıdır:

- `redislabs/rebloom:latest`
- `redis:7.2.5`
- `mcr.microsoft.com/dotnet/aspnet:5.0`

Script image indirmez. Her çalıştırmada benzersiz bir Compose proje adı ve private Docker network oluşturur. Host'a port açmaz; fixture için ayrı test şifreleri kullanır. Testler bittikten sonra veya hata oluştuğunda `finally` içinde o çalışmaya ait Compose container'larını, network'ünü ve anonymous volume'larını temizler.

Projeyi daha önce Release olarak build ettiyseniz scripti `-NoBuild` ile çalıştırabilirsiniz.

Gerekli NuGet paketleri lokaldeki cache'de varsa offline restore seçeneği:

```powershell
dotnet restore Tests/DMicroservices.Redis.Compatibility --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
```

Bu restore yalnızca cache'deki paketleri kullanır; güvenlik açığı taraması yapmaz.

## Test kapsamı

- Authentication kullanan standalone Redis ve Sentinel bağlantıları; Redis ve Sentinel için farklı şifreler.
- String, Hash, TTL ve RedisBloom işlemleri.
- Bağlanılan master'ın doğru seçilmesi ve key scan.
- Çağıran kodun verdiği connection options değerlerinin korunması.
- DB0 ve DB15 arasında izolasyon.
- `RedisManagerV2` ve `RedLockManager` üzerinden işlemler.
- Sentinel `FAILOVER` sonrasında mevcut factory, manager, DB15 ve lock bağlantılarıyla tekrar işlem yapılabilmesi.

Test key'leri benzersiz prefix ve beş dakikalık TTL kullanır. `FLUSHALL` çalıştırılmaz.

## dotnet test ile kendi test ortamınızda çalıştırma

Normal `dotnet test` çalışmasında iki integration testi de skip edilir. Etkinleştirmek için `REDIS_TEST_ENABLE=1` ve `REDIS_URL` verilmelidir. Sentinel failover testi ayrıca `REDIS_TEST_ALLOW_FAILOVER=1` ve bağlantı bilgisinde bir Sentinel `serviceName` gerektirir. Sentinel şifresi için `REDIS_SENTINEL_PASS` kullanılır.

Failover testi seçilen test ortamında master değişimini tetikler. Bu nedenle bu seçeneği geçici test ortamı için kullanın. Docker scripti gerekli ayarları kendi fixture'ı için otomatik verir.

Environment variable'ları ayarladıktan sonra, lokalde .NET 5 runtime ile:

```powershell
dotnet test Tests/DMicroservices.Redis.Compatibility --no-restore -p:GeneratePackageOnBuild=false
```

Her etkin test ayrı bir process başlatır; static configuration ve singleton durumu senaryolar arasında paylaşılmaz.

## Son doğrulama ve kapsam sınırı

Standalone Redis ve Sentinel failover senaryoları Linux .NET **5.0.17**, RedisBloom data server **7.2.4**, Sentinel **7.2.5** ve StackExchange.Redis **2.2.4** ile başarılı oldu. Sentinel ve Redis şifreleri farklıydı. Failover sırasında master değişti; mevcut bağlantılar üzerinden yazma ve lock işlemleri tekrar başarılı oldu.

İki xUnit integration testi de gerekli environment variable'lar verilerek Linux .NET 5.0.17 üzerinde Docker fixture içinde çalıştırıldı: standalone testi **1/1**, Sentinel failover testi **1/1** başarılı; **0 skip**. Sonuçlar `TestResults/standalone.trx` ve `TestResults/sentinel.trx` dosyalarında saklanıyor. Varsayılan `dotnet test` çalışmasında fixture ayarları verilmezse iki test de tasarım gereği skip edilir.

LEDMS'e özel `ElasticUtils`, index temizleme işleri ve Kubernetes pod ayarları bu projenin kapsamında değildir. Diğer testlerin kullanımı için [genel test dokümanına](../ELASTIC-TESTS.md) bakabilirsiniz.
