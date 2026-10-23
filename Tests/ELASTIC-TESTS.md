# Testlerin kullanımı ve kapsamı

Test projeleri solution içindeki `Test` klasöründe yer alır. Elasticsearch testleri Visual Studio Test Explorer ve `dotnet test` ile çalıştırılabilir. Test projeleri .NET 5'i hedefler; xUnit 2.4.2, VSTest adapter 2.4.5 ve Test SDK 17.5.0 kullanır. DMicroservices kütüphanesinin target framework'ü değiştirilmemiştir.

Build için bilgisayarınızda .NET SDK, yerel testleri çalıştırmak için .NET 5 runtime bulunmalıdır. Docker scriptleri test uygulamasını Linux .NET 5 container'ında çalıştırır. Elasticsearch scripti TLS fixture dosyalarını hazırlarken lokaldeki .NET 5 runtime'ı da kullanır.

Aşağıdaki komutları repository'nin ana klasöründe çalıştırın.

## Yerel testleri çalıştırma

Önce ilgili projenin NuGet bağımlılıklarını `dotnet restore <proje yolu>` ile hazırlayın. Ardından:

```powershell
dotnet test Tests/DMicroservices.Elastic.Regression --no-restore -p:GeneratePackageOnBuild=false
dotnet test Tests/DMicroservices.Elastic.Smoke --no-restore -p:GeneratePackageOnBuild=false
dotnet test Tests/DMicroservices.Elastic.Tls.Regression --no-restore -p:GeneratePackageOnBuild=false
dotnet test DMicroservices.Utils.Tests --no-restore -p:GeneratePackageOnBuild=false
```

Gerekli paketler lokaldeki NuGet cache'inde zaten varsa offline restore yapabilirsiniz:

```powershell
dotnet restore Tests/DMicroservices.Elastic.Regression --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
dotnet restore Tests/DMicroservices.Elastic.Smoke --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
dotnet restore Tests/DMicroservices.Elastic.Tls.Regression --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false
```

Bu komutlar yalnızca cache'deki paketleri kullanır. `NuGetAudit=false` nedeniyle bu restore sırasında paketlerin güvenlik açığı taraması yapılmaz.

## Yerel testler neyi kontrol ediyor?

| Proje | Kapsam |
| --- | --- |
| Elastic.Regression | URI ve environment variable üzerinden credentials, index yönlendirmesi, loglarda hassas bilgilerin gizlenmesi, dosya loglarının flush edilmesi, eşzamanlı shutdown ve template oluşturma hatası |
| Elastic.Smoke | Lokalde çalışan Kestrel HTTP fixture üzerinden typeless bulk, compatibility header'ları, üç log yönlendirmesi, o test çalışmasına ait kayıtların ulaşması, index yönlendirmesi çakışması, HTTP 403, Silent modunda template hatasından sonra toparlanma ve beklenen Elasticsearch sürümünün uyuşmaması |
| Elastic.Tls.Regression | Production CA validation callback'i: sistemin güvendiği sertifika, farklı CA, eksik sertifika, hostname uyuşmazlığı, süresi dolmuş sertifika ve server EKU kontrolleri |

Smoke testlerindeki HTTP fixture, Elasticsearch 8 bulk/search yanıtlarını taklit eder. Bu nedenle yerel testleri çalıştırmak için Elasticsearch kurulumu veya Docker gerekmez. Elasticsearch 7/8 ile gerçek uyumluluk aşağıdaki integration testlerinde kontrol edilir.

HTTP fixture kullanan testlerde `Category=LocalHttpFixture` bulunur. Bu testleri hariç tutmak için ilgili `dotnet test` komutuna `--filter "Category!=LocalHttpFixture"` ekleyebilirsiniz.

TLS testleri geçici sertifikaları .NET cryptography API'leriyle üretir. OpenSSL veya Python gerekmez; işletim sisteminin certificate store'u değiştirilmez. Bu testler callback davranışını kontrol eder. Gerçek Linux TLS handshake'i ve NEST üzerinden servis bağlantısı Docker integration testlerinde kontrol edilir. Cluster durability bu testlerin kapsamında değildir.

Test uygulaması her senaryo için ayrı bir process başlatır. Böylece logger singleton'ı, environment variable'lar ve `CloseAndFlush` sonrası durum diğer senaryolara taşınmaz. `ProbeRunner` çalışma süresini sınırlar, hata çıktısını toplar ve üst process'ten gelen Elasticsearch endpoint/credential ayarlarını temizler.

Dosya ve TLS testleri işletim sisteminin geçici klasöründe benzersiz `elastic-regression-*` / `elastic-tls-regression-*` klasörleri oluşturur. Hata incelemesi için bu dosyalar saklanır.

Solution'ın tamamına `dotnet test DMicroservices.sln` çalıştırmadan önce DataAccess ve RabbitMq testlerinin gereksinimlerini inceleyin; bu projeler gerçek servis bağlantısı gerektirebilir.

## Docker ile Redis/Sentinel ve Elasticsearch 7/8 testleri

Bunlar gerçek servislerle çalışan integration testleridir. PowerShell 7 (`pwsh`), çalışan Docker Desktop ve ilgili README'lerde belirtilen Docker image'ları gerekir. Scriptler image indirmez; gerekli image'lar Docker'da önceden bulunmalıdır.

İlk kullanımda bağımlılıkları restore edip iki scripti birlikte çalıştırabilirsiniz:

```powershell
dotnet restore Tests/DMicroservices.Redis.Compatibility
dotnet restore Tests/DMicroservices.Elastic.Smoke
pwsh -File Tests/Run-Compatibility.ps1
```

Script önce Redis standalone ve Sentinel failover senaryolarını, ardından Elasticsearch 7.16.1 ve 8.15.0 senaryolarını çalıştırır. Her senaryo kendi Docker Compose ortamını kullanır. Servisler başlatılır, kontroller yapılır ve test hata verse bile ilgili Compose ortamı `finally` içinde temizlenir. Host'a port açılmaz.

Scriptler projeleri Release olarak build eder; NuGet restore yapmaz. Projeler daha önce Release olarak build edildiyse `-NoBuild` kullanılabilir.

| Test grubu | Gerçek servis üzerinden kontrol edilenler | Kullanım detayı |
| --- | --- | --- |
| Redis / Sentinel | Redis işlemleri, DB izolasyonu, ayrı Redis/Sentinel şifreleri, master seçimi, failover sonrası mevcut bağlantıların toparlanması ve distributed lock | [Redis/Sentinel README](DMicroservices.Redis.Compatibility/README.md) |
| Elasticsearch 7 / 8 | Gerçek sürüm kontrolü, HTTPS, Basic Auth, özel CA, log yönlendirmesi, flush/search ve ElasticHelper ile yazma/okuma | [Elasticsearch README](DMicroservices.Elastic.Smoke/README.md) |

Redis/Sentinel testleri LEDMS'teki `ElasticMigration` klasöründen DMicroservices'e taşındı ve doğrudan paket kaynak kodunu referans alıyor. Normal `dotnet test` çalışmasında Redis integration testleri gerekli environment variable'lar verilmedikçe skip edilir.

Bu senaryolar bileşenleri ayrı ayrı test eder; her Redis topolojisi ile her Elasticsearch sürümünün birlikte çalıştığı tüm kombinasyonlar denenmez. LEDMS'e özel index temizleme işleri ve Kubernetes pod ayarları paket testlerinin kapsamı dışındadır.

## Smoke uygulamasını kendi test cluster'ınızda çalıştırma

Geçici bir test cluster'ı için `ELASTIC_URI` ve `LOG_INDEX_FORMAT` ayarlayın. Gerekiyorsa `ELASTIC_USERNAME`, `ELASTIC_PASSWORD` ve `ELASTIC_URI_SSL` değerlerini de verin:

```powershell
dotnet run --project Tests/DMicroservices.Elastic.Smoke --no-build
```

Bu komut belirtilen cluster'a üç log kaydı gönderir ve kayıtları search ile kontrol eder. Önce projeyi build etmiş olmalısınız.

## Son doğrulama

Yerel Elasticsearch testleri: Regression **2/2**, Smoke **6/6**, TLS **1/1** başarılı.

Docker integration testleri: Redis standalone, Sentinel failover, Elasticsearch **7.16.1** ve **8.15.0** başarılı. Redis xUnit integration testleri de Docker fixture ile etkinleştirilerek çalıştırıldı: **2/2 başarılı, 0 skip**. Fixture ayarları verilmeden yapılan varsayılan `dotnet test` çalışmasında bu iki test tasarım gereği skip edilir.
