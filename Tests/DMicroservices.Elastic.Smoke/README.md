# Elasticsearch 7 / 8 testlerinin kullanımı

Bu script gerçek Elasticsearch **7.16.1** ve **8.15.0** servislerini Docker'da sırayla başlatıp aynı DMicroservices koduyla test eder. Yerel Smoke testleri ise Kestrel HTTP fixture kullanır ve Docker başlatmaz.

## Docker ile çalıştırma

PowerShell 7 (`pwsh`), çalışan Docker Desktop, bilgisayarınızda .NET SDK ve .NET 5 runtime gerekir. Repository'nin ana klasöründe:

```powershell
dotnet restore Tests/DMicroservices.Elastic.Smoke
pwsh -File Tests/DMicroservices.Elastic.Smoke/Run-ElasticCompatibility.ps1
```

Script Smoke projesini Release olarak build eder ve .NET ile geçici TLS sertifikaları üretir. Her Elasticsearch sürümü kendi private Docker network'ü ve sıfırdan oluşturulan verisiyle çalışır. Host'a port açılmaz.

Docker'da şu image'lar önceden bulunmalıdır:

- `docker.elastic.co/elasticsearch/elasticsearch:7.16.1`
- `docker.elastic.co/elasticsearch/elasticsearch:8.15.0`
- `mcr.microsoft.com/dotnet/aspnet:5.0`

Script image indirmez. Servisin başlatılması veya test başarısız olursa hata çıktısı gösterilir. İlgili Compose ortamı `finally` içinde temizlenir. TLS fixture dosyaları hata incelemesi için Git tarafından ignore edilen `TestResults` klasöründe saklanır.

Projeyi daha önce Release olarak build ettiyseniz scripti `-NoBuild` ile çalıştırabilirsiniz.

Gerekli NuGet paketleri lokaldeki cache'de varsa offline restore seçeneği:

```powershell
dotnet restore Tests/DMicroservices.Elastic.Smoke --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
```

Bu restore yalnızca cache'deki paketleri kullanır; güvenlik açığı taraması yapmaz.

## Sürüm uyumluluğu nasıl kontrol ediliyor?

Test uygulaması Linux .NET 5.0.17 container'ında çalışır. İlk adımda authentication ile bağlanıp sunucunun gerçek sürümünü okur ve `COMPAT_EXPECTED_ELASTIC` ile karşılaştırır. Böylece 7.16.1 senaryosunun gerçekten 7.16.1'e, 8.15.0 senaryosunun gerçekten 8.15.0'a bağlandığı doğrulanır.

Sürüm uyuşmazsa log yazılmadan test başarısız olur. Yerel xUnit Smoke testlerinde bu hata durumu HTTP fixture ile ayrıca kontrol edilir.

Sürüm doğrulamasından sonra aynı test uygulaması her iki sürüm için şunları kontrol eder:

- `info`, `error` ve `specific` log kayıtlarının doğru index'lere gitmesi.
- `CloseAndFlush` sonrası o test çalışmasına ait kayıtların search ile bulunması.
- `COMPAT_VERIFY_HELPER=1` ile paketin `ElasticHelper/NEST` üzerinden yazma, refresh ve search işlemleri.
- HTTPS, Basic Auth ve test için üretilen özel CA ile bağlantı. CA kontrolünde production certificate validation callback'i kullanılır.

Fixture, CA trust için ayrı bir PEM sertifikasını `certificate_authorities` ayarına verir. Ayarın açıklaması [Elastic security settings dokümanında](https://www.elastic.co/docs/reference/elasticsearch/configuration-reference/security-settings) bulunur.

## Redis/Sentinel ile birlikte çalıştırma

İki test projesinin NuGet bağımlılıkları restore edilmiş olmalıdır. Repository'nin ana klasöründe:

```powershell
pwsh -File Tests/Run-Compatibility.ps1
```

Bu script Redis standalone, Sentinel failover ve iki Elasticsearch sürümünü sırayla kontrol eder. Bunlar ayrı bileşen senaryolarıdır; tüm Redis topolojisi / Elasticsearch sürümü kombinasyonları birlikte denenmez. LEDMS'e özel utility'ler, index silme işleri ve Kubernetes ayarları paket testlerinin kapsamı dışındadır.

## Son doğrulama

Gerçek servislerle Linux .NET **5.0.17** üzerinde:

| Elasticsearch | HTTPS / Basic Auth / özel CA | Log yönlendirmesi / flush / search | ElasticHelper yazma / okuma |
| --- | --- | --- | --- |
| 7.16.1 | Başarılı | Başarılı | Başarılı |
| 8.15.0 | Başarılı | Başarılı | Başarılı |

Yerel .NET 5 test projelerinde de Regression **2/2**, Smoke **6/6**, TLS **1/1** başarılı oldu. Diğer testlerin kullanımı için [genel test dokümanına](../ELASTIC-TESTS.md) bakabilirsiniz.
