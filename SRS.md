# Sims 3 ↔ Sims 4 Custom Content Converter

**Software Requirements Specification (SRS)**
**Doküman Sürümü:** 0.1
**Proje Durumu:** Başlangıç / Discovery
**Hedef Platform:** Windows Desktop
**Geliştirme Ortamı:** macOS
**UI Teknolojisi:** Avalonia UI 12
**Runtime:** .NET 10 / C#
**Mimari:** Desktop Application + Conversion Engine + Plugin/Adapter Architecture

---

# 1. Amaç

Bu projenin amacı The Sims 3 ve The Sims 4 için hazırlanmış Custom Content (CC) bileşenlerini iki oyun arasında dönüştürmeyi kolaylaştıran bir Windows masaüstü uygulaması geliştirmektir.

Uygulama başlangıçta aşağıdaki içerik sınıflarını hedefleyecektir:

* CAS kıyafetleri
* Aksesuarlar
* Saçlar
* Basit dekoratif objeler
* Mobilyalar
* Texture/recolor içerikleri

Uzun vadeli hedef:

**Sims 3 → Sims 4** ve **Sims 4 → Sims 3** dönüşümlerinde kullanıcı tarafından bugün Blender, Sims 4 Studio, TSR Workshop ve çeşitli package araçları kullanılarak manuel yapılan işlemlerin mümkün olduğunca otomatikleştirilmesi.

Uygulama ticari oyun dosyalarını veya üçüncü kişilere ait içerikleri dağıtmayacaktır.

---

# 2. Ürün Vizyonu

Kullanıcı bir `.package` veya desteklenen başka bir CC dosyasını uygulamaya bırakabilmelidir.

Uygulama:

1. Kaynak oyunu tespit eder.
2. Package içeriğini analiz eder.
3. İçindeki mesh, texture, metadata ve diğer resource bileşenlerini belirler.
4. Dönüşüm uygunluğunu analiz eder.
5. Gerekli resource eşlemelerini oluşturur.
6. Mesh ve texture dönüşümlerini gerçekleştirir.
7. Hedef oyun için gerekli metadata/resource yapılarını oluşturur.
8. Ortaya çıkan içeriği doğrular.
9. Kullanıcıya dönüşüm raporu gösterir.
10. Hedef oyuna uygun yeni bir dosya oluşturur.

Ana kullanıcı deneyimi mümkün olduğunca:

**Dosya seç → hedef oyunu seç → analiz et → convert → test et**

şeklinde olmalıdır.

---

# 3. Temel Tasarım Prensibi

Dönüşüm motoru deterministik olacaktır.

AI/LLM bir `.package` dosyasını doğrudan değiştiren ana motor olmayacaktır.

AI aşağıdaki alanlarda yardımcı olacaktır:

* bilinmeyen resource tiplerini açıklamak,
* eşleme önerileri yapmak,
* dönüşüm hatalarını kullanıcıya anlaşılır biçimde açıklamak,
* uygun target template önermek,
* metadata kategorilerini eşlemek,
* dönüşüm workflow'unu yönetmek,
* kullanıcıya düzeltme önerileri sunmak.

Binary parsing, mesh transformation, texture conversion ve package generation işlemleri test edilebilir C# servisleri tarafından yapılacaktır.

---

# 4. Kullanıcı Profili

Birincil kullanıcı:

* Sims 3 ve Sims 4 oynayan,
* Custom Content kullanan,
* Blender veya Sims modding araçlarını profesyonel seviyede bilmeyen,
* mevcut CC içeriklerini diğer Sims sürümüne taşımak isteyen kullanıcı.

İkincil kullanıcı:

* Sims CC creator,
* Blender kullanan mod geliştiricisi,
* toplu CC migration yapmak isteyen ileri seviye kullanıcı.

---

# 5. Hedef Platformlar

## 5.1 Runtime Platform

İlk production sürümü:

* Windows 10 22H2+
* Windows 11
* x64

İleri sürümlerde:

* Windows ARM64
* macOS

değerlendirilebilir.

## 5.2 Development Platform

Ana geliştirme ortamı:

* macOS Apple Silicon
* JetBrains Rider veya VS Code
* .NET 10 SDK

Windows production build'i macOS üzerinden alınabilmelidir.

---

# 6. Teknoloji Yığını

## Desktop UI

* Avalonia UI 12
* C#
* XAML
* MVVM

## Runtime

* .NET 10

## Dependency Injection

* Microsoft.Extensions.DependencyInjection

## Logging

* Microsoft.Extensions.Logging
* Serilog

## Configuration

* Microsoft.Extensions.Configuration

## Testing

* xUnit
* FluentAssertions

## Optional

* CommunityToolkit.Mvvm

## 3D / Asset Processing

İlk araştırma sonucuna göre aşağıdaki seçenekler adapter olarak değerlendirilmelidir:

* AssimpNet
* SharpGLTF
* custom GEOM readers/writers
* Blender headless CLI integration

Blender uygulamanın zorunlu runtime dependency'si olmamalıdır.

Ancak bazı karmaşık mesh dönüşümlerinde optional external processing engine olarak kullanılabilmelidir.

---

# 7. Mimari

Solution aşağıdaki ana projelere ayrılmalıdır.

```text
SimsConverter.sln

src/
  SimsConverter.App/
  SimsConverter.Application/
  SimsConverter.Domain/
  SimsConverter.Infrastructure/

  SimsConverter.Package/
  SimsConverter.Sims3/
  SimsConverter.Sims4/

  SimsConverter.Mesh/
  SimsConverter.Textures/
  SimsConverter.Conversion/

  SimsConverter.AI/
  SimsConverter.Validation/

tests/
  SimsConverter.Domain.Tests/
  SimsConverter.Package.Tests/
  SimsConverter.Sims3.Tests/
  SimsConverter.Sims4.Tests/
  SimsConverter.Mesh.Tests/
  SimsConverter.Conversion.Tests/
  SimsConverter.IntegrationTests/

tools/
  CorpusInspector/
  PackageDumper/
```

---

# 8. Katmanların Sorumlulukları

## SimsConverter.App

Avalonia UI.

Sorumlulukları:

* dosya seçme
* drag & drop
* conversion wizard
* preview ekranları
* progress gösterimi
* validation sonuçları
* log görüntüleme
* settings
* batch conversion

Bu proje package format bilgisi içermemelidir.

---

## SimsConverter.Domain

Platform ve Sims sürümünden bağımsız domain modellerini içerir.

Örnek modeller:

```text
GameAsset
MeshAsset
TextureAsset
MaterialAsset
SkeletonAsset
BoneWeight
LodAsset
CatalogMetadata
CasMetadata
ObjectMetadata
ConversionJob
ConversionIssue
ConversionReport
```

---

## SimsConverter.Package

EA DBPF/package container seviyesindeki işlemler.

Sorumlulukları:

* package header parsing
* resource index parsing
* resource extraction
* compression/decompression
* resource replacement
* package creation
* package validation

Sims 3 ve Sims 4'e özel semantic interpretation bu katmana konulmamalıdır.

---

## SimsConverter.Sims3

The Sims 3 format adapter.

Sorumlulukları:

* Sims 3 resource type tanımları
* CAS metadata
* object metadata
* mesh parsing
* texture references
* skeleton/rig metadata
* material ilişkileri
* Sims 3 package generation

---

## SimsConverter.Sims4

The Sims 4 format adapter.

Sorumlulukları:

* Sims 4 resource type tanımları
* CAS metadata
* GEOM parsing
* texture resource parsing
* material metadata
* rig/skeleton data
* LOD metadata
* Sims 4 package generation

---

## SimsConverter.Mesh

Oyunlardan bağımsız canonical mesh modeli.

Örneğin:

```text
CanonicalMesh
    Vertices
    Normals
    Tangents
    UV0
    UV1
    Faces
    BoneIndices
    BoneWeights
    Materials
```

Hem Sims 3 hem Sims 4 meshleri önce bu intermediate representation'a çevrilmelidir.

Daha sonra hedef format oluşturulmalıdır.

Bu sayede:

```text
TS3 Mesh
   ↓
Canonical Mesh
   ↓
TS4 Mesh
```

ve ters yönde:

```text
TS4 Mesh
   ↓
Canonical Mesh
   ↓
TS3 Mesh
```

aynı pipeline kullanılabilir.

---

# 9. Canonical Asset Model

Dönüşüm doğrudan Sims3Resource → Sims4Resource biçiminde yapılmamalıdır.

Ara format kullanılmalıdır.

```text
Source Package
      ↓
Package Parser
      ↓
Source Game Adapter
      ↓
Canonical Asset Model
      ↓
Transformation Pipeline
      ↓
Target Game Adapter
      ↓
Target Package Writer
```

Canonical Asset Model aşağıdaki bilgileri mümkün olduğu kadar kayıpsız saklamalıdır:

* vertex geometry
* triangles
* normals
* tangents
* UV channels
* bone assignments
* vertex weights
* materials
* diffuse texture
* normal texture
* specular texture
* masks
* LOD definitions
* catalog metadata
* CAS metadata
* object metadata
* bounding box
* slots
* source resource IDs

---

# 10. Conversion Pipeline

Her conversion aşağıdaki pipeline üzerinden çalışmalıdır.

## Stage 1 — Inspect

Dosya açılır.

Tespit edilir:

* package türü
* kaynak oyun
* içerik türü
* resource listesi
* dependency listesi

---

## Stage 2 — Extract

Package içerisinden ilgili kaynaklar çıkarılır.

Örneğin:

* meshes
* textures
* CAS metadata
* materials
* rigs
* thumbnails

---

## Stage 3 — Normalize

Kaynak veriler Canonical Asset Model'e çevrilir.

---

## Stage 4 — Compatibility Analysis

Conversion engine aşağıdaki problemleri tespit eder:

* unsupported resource
* missing mesh
* missing texture
* missing bone
* incompatible skeleton
* invalid UV
* unsupported shader
* excessive polygon count
* missing LOD
* missing target category
* incompatible object behavior

---

## Stage 5 — Transform

Gerekli dönüşümler uygulanır.

Örnek:

* coordinate conversion
* scaling
* mesh transform
* UV conversion
* bone mapping
* weight normalization
* texture conversion
* LOD generation
* metadata translation

---

## Stage 6 — Target Template Selection

Bazı asset'lerde hedef oyunda referans/base resource gerekecektir.

Örneğin:

```text
TS3 Chair
→ TS4 Chair Template

TS4 Dress
→ TS3 Full Body Clothing Template
```

Template seçim motoru hedef oyunda uygun base asset'i bulmalıdır.

---

# 11. Template System

Template sistemi conversion projesinin kritik parçalarından biridir.

Template şunları sağlayabilir:

* skeleton
* slot configuration
* catalog category
* shader setup
* default material
* object behavior
* interaction metadata
* CAS category

Template registry oluşturulmalıdır.

Örnek:

```text
templates/
  sims3/
    cas/
      adult_female_top.json
      adult_female_fullbody.json

    objects/
      chair.json
      table.json

  sims4/
    cas/
      adult_female_top.json
      adult_female_fullbody.json

    objects/
      chair.json
      table.json
```

EA oyun dosyalarının kendileri repository içerisinde dağıtılmamalıdır.

Template tanımları gerektiğinde kullanıcının yerel oyun kurulumundan oluşturulmalıdır.

---

# 12. Texture Conversion

Texture pipeline aşağıdaki temel formatları desteklemelidir:

* DDS
* PNG intermediate
* game-specific compressed texture resources

Canonical texture format mümkünse:

```text
RGBA8888
```

veya gerektiğinde DDS intermediate olabilir.

Pipeline:

```text
Source texture
↓
Decode
↓
Canonical bitmap
↓
Resize / Channel mapping
↓
Target compression
↓
Target texture resource
```

Desteklenmesi gereken map kategorileri:

* diffuse
* normal
* specular
* alpha
* mask

Channel mapping kuralları configurable olmalıdır.

---

# 13. Mesh Conversion

Mesh conversion aşağıdaki operasyonları desteklemelidir:

* coordinate-system transformation
* scaling
* rotation
* normal conversion
* tangent generation
* UV remapping
* vertex deduplication
* vertex merge
* bone remapping
* weight normalization
* polygon validation
* LOD generation

Mesh conversion sonrası aşağıdaki kontroller yapılmalıdır:

```text
NaN vertices = 0
Invalid triangles = 0
Missing normals = 0
Bone weights > allowed maximum = 0
UV outside accepted range = warning
```

---

# 14. CAS Clothing Conversion

Kıyafet conversion en zor senaryolardan biri kabul edilmelidir.

Pipeline:

```text
Extract source clothing
↓
Extract mesh
↓
Extract textures
↓
Identify age/gender/category
↓
Select target body/template
↓
Align mesh
↓
Map skeleton
↓
Transfer/adapt bone weights
↓
Adapt UV
↓
Generate LODs
↓
Convert textures
↓
Generate target CAS metadata
↓
Build package
↓
Validate
```

İlk MVP'de şu CAS kategorileri hedeflenmelidir:

* Top
* Bottom
* Full Body

Daha sonra:

* Shoes
* Hats
* Accessories
* Hair

eklenmelidir.

---

# 15. Object Conversion

Object conversion kıyafetlerden ayrı pipeline kullanmalıdır.

İlk MVP'de yalnızca basit dekoratif objeler desteklenmelidir.

Örnek:

* sculpture
* clutter
* decoration

İkinci faz:

* chair
* table
* lamp

Daha sonraki faz:

* doors
* windows
* counters
* beds
* animated objects

İnteraktif objeler ilk MVP kapsamına alınmamalıdır.

Sebebi:

Sims 3 ve Sims 4 object behavior/tuning sistemleri arasında yalnızca mesh/texture dönüşümüyle çözülemeyecek oyun mantığı farklılıkları bulunmaktadır.

---

# 16. LOD Sistemi

Her asset için LOD bilgisi tutulmalıdır.

Örnek:

```text
LOD0
LOD1
LOD2
LOD3
```

Kaynak içerikte yeterli LOD bulunmuyorsa application otomatik LOD üretmeyi teklif etmelidir.

İlk implementation:

* polygon decimation
* preserve UV
* preserve material
* transfer bone weights

LOD kalite hedefleri configurable olmalıdır.

---

# 17. Rig / Skeleton Mapping

Rig mapping ayrı bir subsystem olmalıdır.

```text
IRigMapper
```

Implementasyonlar:

```text
Sims3ToSims4RigMapper
Sims4ToSims3RigMapper
```

Bone mapping JSON tablolarından okunabilmelidir.

Örneğin:

```json
{
  "sourceBone": "b__Spine1__",
  "targetBone": "...",
  "strategy": "Direct"
}
```

Mapping strategy:

* Direct
* ParentFallback
* NearestEquivalent
* Ignore
* ManualRequired

Bone eşleşmesi bulunamadığında conversion sessizce devam etmemelidir.

Warning veya blocking error oluşturmalıdır.

---

# 18. AI Agent Katmanı

AI özellikleri core conversion engine'den bağımsız olacaktır.

Interface:

```text
IAiConversionAssistant
```

AI aşağıdaki fonksiyonları sağlayabilir.

## ExplainIssue

Örnek:

```text
"Mesh üzerinde hedef Sims 4 skeleton'ında karşılığı bulunmayan
3 bone bulundu."
```

AI bunu kullanıcıya sade biçimde açıklayabilir.

---

## SuggestTemplate

Asset metadata ve geometry analizine göre uygun hedef template önerebilir.

---

## SuggestCategoryMapping

Örneğin:

```text
TS3 Everyday / Formal top
→
TS4 CAS Upper Body / Everyday + Formal
```

---

## Conversion Diagnosis

Başarısız conversion loglarını analiz ederek öneriler sunabilir.

---

## Agent Guardrail

AI hiçbir zaman:

* binary package writer yerine geçmemeli,
* doğrulanmamış resource ID üretmemeli,
* kullanıcı onayı olmadan source file üzerine yazmamalı,
* oyun kurulumundaki original dosyaları değiştirmemelidir.

---

# 19. Kullanıcı Arayüzü

Ana ekran sade olmalıdır.

```text
┌───────────────────────────────────────────┐
│ Sims CC Converter                        │
│                                           │
│      Drop a .package file here           │
│                                           │
│             [ Browse ]                    │
│                                           │
└───────────────────────────────────────────┘
```

Dosya yüklendikten sonra:

```text
Source:
The Sims 3

Asset Type:
Female / Adult / Full Body Clothing

Resources:
✓ Mesh
✓ Diffuse
✓ Normal
✓ CAS Metadata
✓ LODs

Target:
The Sims 4

Compatibility:
82%

[ Analyse ]       [ Convert ]
```

---

# 20. Conversion Wizard

Adımlar:

## Step 1

Source asset

## Step 2

Target game

## Step 3

Asset analysis

## Step 4

Target template

## Step 5

Conversion options

## Step 6

Validation

## Step 7

Export

---

# 21. Advanced Mode

İleri kullanıcılar için ayrı panel bulunmalıdır.

Burada kullanıcı görebilmelidir:

* resource list
* Type
* Group
* Instance
* resource size
* mesh statistics
* texture statistics
* bone mappings
* LOD mappings
* conversion log

Resource inspector kullanılabilmelidir.

---

# 22. Preview

Mümkünse ilerleyen sürümlerde basit 3D preview eklenmelidir.

Preview:

* rotate
* zoom
* wireframe
* textured view
* LOD selection

desteklemelidir.

İlk MVP için 3D preview zorunlu değildir.

---

# 23. Batch Conversion

Uygulama birden fazla package dosyasını dönüştürebilmelidir.

Örnek:

```text
47 files selected

32 Convertible
8 Convertible with warnings
7 Unsupported
```

Kullanıcı yalnızca uygun dosyaları convert edebilmelidir.

---

# 24. Dosya Güvenliği

Application kaynak dosyayı hiçbir zaman overwrite etmemelidir.

Default output:

```text
Converted/
```

Örneğin:

```text
dress.package

→

Converted/
    dress_TS4.package
```

Atomic write kullanılmalıdır.

Önce:

```text
dress_TS4.package.tmp
```

oluşturulmalı, validation başarılı olduktan sonra final dosyaya dönüştürülmelidir.

---

# 25. Game Installation Detection

Application isteğe bağlı olarak Sims kurulumlarını bulabilmelidir.

Desteklenecek launcher'lar:

* EA App
* Steam

Bulunamadığında kullanıcı manuel klasör seçebilmelidir.

Application oyun installation dosyalarını read-only kullanmalıdır.

---

# 26. Mods Folder Integration

Kullanıcı isterse output package otomatik olarak Mods klasörüne kopyalanabilmelidir.

Bu özellik default olarak kapalı olacaktır.

Conversion tamamlandığında:

```text
Open Output Folder

Install to Mods Folder
```

seçenekleri gösterilebilir.

---

# 27. Validation Engine

Her conversion sonucunda validation raporu üretilmelidir.

Severity seviyeleri:

```text
Info
Warning
Error
Fatal
```

Örnek kontroller:

* package structure valid
* required resources present
* texture decodable
* mesh readable
* mesh topology valid
* skeleton references valid
* weights normalized
* valid target category
* valid LOD references
* no duplicate critical IDs

---

# 28. Conversion Report

Her conversion sonunda JSON report oluşturulabilmelidir.

Örnek:

```json
{
  "sourceGame": "Sims3",
  "targetGame": "Sims4",
  "assetType": "CAS.FullBody",
  "status": "ConvertedWithWarnings",
  "warnings": [],
  "errors": [],
  "output": "dress_TS4.package"
}
```

---

# 29. Logging

Structured logging kullanılmalıdır.

Log içerisinde hiçbir zaman:

* kişisel kullanıcı bilgisi
* API key
* token

bulunmamalıdır.

Development mode ayrıntılı log tutmalıdır.

Production mode minimum gerekli bilgiyi saklamalıdır.

---

# 30. Plugin Architecture

Yeni asset türleri uygulamaya plugin biçiminde eklenebilmelidir.

Interface örnekleri:

```text
IGameAdapter
IPackageReader
IPackageWriter
IAssetExtractor
IAssetConverter
IAssetValidator
ITextureCodec
IMeshCodec
IRigMapper
ITemplateResolver
```

Bu yapı Sims format bilgisinin UI'dan ayrılmasını sağlar.

---

# 31. Conversion Registry

Converter discovery registry üzerinden yapılmalıdır.

Örnek:

```text
TS3.CAS.Top → TS4.CAS.Top
TS3.CAS.Bottom → TS4.CAS.Bottom
TS3.CAS.FullBody → TS4.CAS.FullBody

TS4.CAS.Top → TS3.CAS.Top
TS4.CAS.Bottom → TS3.CAS.Bottom
TS4.CAS.FullBody → TS3.CAS.FullBody
```

Her converter kendi capability bilgisini yayınlamalıdır.

---

# 32. Unsupported Conversion Davranışı

Application hiçbir zaman desteklenmeyen conversion için sahte bir başarılı sonuç üretmemelidir.

Örneğin:

```text
Sims 3 Functional Elevator
→ Sims 4
```

desteklenmiyorsa:

```text
Unsupported Asset

Mesh and textures can potentially be extracted,
but gameplay behavior cannot currently be converted.
```

mesajı gösterilmelidir.

---

# 33. Copyright / Asset Ownership

Application üçüncü kişilere ait CC içeriğinin izinsiz dağıtılmasını teşvik etmemelidir.

Kullanıcıya ilk çalıştırmada şu prensip açıklanmalıdır:

Kullanıcı yalnızca dönüştürme hakkına sahip olduğu içerikleri dönüştürmelidir.

Application:

* EA asset paketlerini dağıtmamalı,
* copyrighted game resources repository'ye dahil etmemeli,
* kullanıcının game installation dosyalarını upload etmemelidir.

---

# 34. Offline First

Core conversion tamamen offline çalışabilmelidir.

Aşağıdaki işlemler internet gerektirmemelidir:

* package inspection
* mesh extraction
* texture conversion
* conversion
* validation
* package creation

AI Assistant internet/API gerektirebilir ve opsiyonel olmalıdır.

AI kapalı olduğunda core application çalışmaya devam etmelidir.

---

# 35. AI Provider Abstraction

AI provider hard-coded olmamalıdır.

```text
IAiProvider
```

Implementasyon örnekleri:

```text
OpenAiProvider
LocalModelProvider
NoAiProvider
```

API key işletim sisteminin secure credential storage mekanizmasında tutulmalıdır.

Config veya log dosyasına plain text yazılmamalıdır.

---

# 36. Performans Gereksinimleri

Tipik 50 MB altındaki package için inspection işlemi kullanıcı arayüzünü bloklamamalıdır.

Conversion işlemleri background worker üzerinden yürütülmelidir.

UI thread üzerinde:

* package parsing
* texture decompression
* mesh conversion

çalıştırılmamalıdır.

CancellationToken desteklenmelidir.

---

# 37. Crash Recovery

Conversion job ara durumları tutulmalıdır.

Application crash olduğunda kullanıcıya:

```text
Previous conversion did not complete.
```

bilgisi gösterilebilir.

Yarım kalan output dosyaları final package olarak bırakılmamalıdır.

---

# 38. Test Stratejisi

Binary conversion projesi olduğu için snapshot/golden-file testleri kritik olacaktır.

Test corpus oluşturulmalıdır.

Örnek:

```text
TestCorpus/
   sims3/
      cas/
      objects/

   sims4/
      cas/
      objects/
```

Copyright nedeniyle test corpus repository'de public tutulmayabilir.

CI içerisinde private fixture storage kullanılabilir.

---

# 39. Parser Testleri

Her parser için test:

```text
Given valid resource
When parsed
Then expected structure is produced
```

Ayrıca bozuk dosyalar test edilmelidir.

Örneğin:

* truncated package
* invalid index
* unknown compression
* corrupt texture
* invalid vertex count

Application crash etmemelidir.

---

# 40. Round-trip Testleri

Mümkün olan formatlarda:

```text
Read
→ Canonical
→ Write
→ Read
```

işlemi uygulanmalıdır.

Semantic veri korunmalıdır.

---

# 41. Golden Conversion Tests

Bilinen bir source asset ve target sonucu kullanılarak:

```text
TS3 fixture
→ converter
→ TS4 output
```

otomatik test edilmelidir.

Binary dosyanın tamamen byte-identical olması zorunlu değildir.

Semantic validation esas alınmalıdır.

---

# 42. MVP Kapsamı

MVP'nin amacı iki oyun arasındaki tüm Custom Content'i dönüştürmek değildir.

MVP:

### Package Inspector

* TS3 package açabilme
* TS4 package açabilme
* resource listeleme

### Texture

* extract
* preview
* convert
* repackage

### Mesh

* extract
* canonical modele dönüştürme
* mesh statistics
* basic transform

### CAS

İlk olarak:

```text
TS3 → TS4
Top
Bottom
Full Body
```

desteklenmelidir.

Sonrasında ters yön:

```text
TS4 → TS3
```

eklenmelidir.

---

# 43. Önerilen Geliştirme Fazları

## Phase 0 — Research Tooling

Package corpus inceleme.

Resource dumper geliştirme.

TS3/TS4 package farklılıklarını belgelemek.

---

## Phase 1 — Package Inspector

Amaç:

Her iki oyun package dosyasını güvenilir biçimde okuyabilmek.

UI:

```text
Open package
→ list resources
→ export resource
```

Henüz conversion yok.

---

## Phase 2 — Texture Pipeline

Her iki oyunun texture resource'larını okuyup canonical image formatına çevirmek.

---

## Phase 3 — Mesh Pipeline

Meshleri canonical mesh formatına dönüştürmek.

---

## Phase 4 — TS3 → TS4 Decorative Object

İlk gerçek end-to-end conversion.

Basit dekor objesi seçilmesinin nedeni:

* skeleton problemi yok veya minimum,
* CAS body fitting yok,
* gameplay tuning düşük,
* conversion pipeline daha kolay doğrulanabilir.

---

## Phase 5 — TS3 → TS4 Clothing

CAS Top ile başlanmalıdır.

Sonra:

* Bottom
* Full Body

---

## Phase 6 — TS4 → TS3

Aynı pipeline ters yönde oluşturulmalıdır.

---

## Phase 7 — Advanced Assets

* Hair
* Accessories
* Shoes
* Furniture
* Lighting
* complex objects

---

# 44. MVP Dışında Tutulacak Özellikler

İlk sürümde aşağıdakiler garanti edilmemelidir:

* gameplay script conversion
* tuning conversion
* animations
* poses
* careers
* traits
* interactions
* complex doors/windows
* counters
* vehicles
* pets
* automatic perfect rig conversion
* automatic perfect UV conversion

---

# 45. Başarı Kriterleri

Phase 1 başarılı kabul edilir:

* seçilen TS3 ve TS4 fixture package dosyalarının %100'ü crash olmadan açılabiliyorsa,
* resource index doğru okunuyorsa,
* resources extract edilebiliyorsa.

İlk conversion MVP başarılı kabul edilir:

* belirlenen desteklenen asset corpusunun en az %80'i conversion pipeline'dan blocking error olmadan geçiyorsa,
* oluşturulan package hedef oyun/tool tarafından okunabiliyorsa,
* validation engine kritik hata vermiyorsa.

---

# 46. Kodlama Kuralları

AI coding agent dahil tüm geliştiriciler aşağıdaki kurallara uymalıdır.

* Nullable reference types açık.
* Async metotlarda CancellationToken.
* UI katmanında game-specific binary parsing yok.
* Magic resource ID kullanılmaz.
* Resource IDs named constants/enums olarak tanımlanır.
* Binary parsing sırasında bounds checking zorunlu.
* Source package hiçbir zaman overwrite edilmez.
* Her yeni parser için unit test zorunlu.
* Her yeni converter için fixture test zorunlu.
* Unknown resource'lar ignore edilmek yerine raporlanır.
* AI çıktısı validation olmadan binary writer'a verilmez.

---

# 47. AI Agent Development Instructions

Coding agent projede çalışmaya başlamadan önce:

1. Bu SRS'yi oku.
2. Mevcut solution yapısını incele.
3. İlgili proje dışında gereksiz değişiklik yapma.
4. Binary format hakkında tahmin yürütme.
5. Bilinmeyen field/resource gördüğünde `Unknown` olarak modelle.
6. Bir resource ID'nin anlamından emin değilsen documentation/test fixture ile doğrula.
7. Her implementation ile test ekle.
8. Conversion sırasında veri kaybı varsa warning oluştur.
9. Silent fallback kullanma.
10. Büyük refactor öncesinde mevcut testlerin geçtiğini doğrula.

---

# 48. İlk Development Epic'leri

## EPIC-001 — Solution Bootstrap

Acceptance Criteria:

* .NET 10 solution oluşturuldu.
* Avalonia desktop app açılıyor.
* Windows target build alınabiliyor.
* macOS development build çalışıyor.
* CI build çalışıyor.

---

## EPIC-002 — Package Detection

Application dosyanın:

```text
Sims 3
Sims 4
Unknown
```

olduğunu tespit edebilmelidir.

---

## EPIC-003 — DBPF Resource Inspector

Application package içindeki resource index'i görüntülemelidir.

Kolonlar:

```text
Type
Group
Instance
Size
Compression
```

---

## EPIC-004 — Resource Export

Kullanıcı seçilen resource'u raw binary olarak export edebilmelidir.

---

## EPIC-005 — Texture Extraction

Desteklenen texture resource'ları PNG/DDS olarak çıkarılabilmelidir.

---

## EPIC-006 — Canonical Mesh Model

Game-independent mesh modeli oluşturulmalıdır.

---

## EPIC-007 — TS3 Mesh Importer

TS3 mesh → CanonicalMesh.

---

## EPIC-008 — TS4 Mesh Importer

TS4 mesh → CanonicalMesh.

---

## EPIC-009 — Validation Framework

ConversionIssue ve ConversionReport implement edilmelidir.

---

## EPIC-010 — First End-to-End Converter

Basit Sims 3 dekoratif obje:

```text
TS3
→ Canonical
→ TS4
```

pipeline'ından geçirilmelidir.

---

# 49. Definition of Done

Bir conversion feature tamamlanmış sayılması için:

* implementation mevcut,
* unit tests mevcut,
* integration fixture mevcut,
* invalid input test edilmiş,
* cancellation destekleniyor,
* logging mevcut,
* validation mevcut,
* source file değiştirilmiyor,
* generated file tekrar parser tarafından açılabiliyor,
* UI hata durumunu doğru gösteriyor.

---

# 50. Nihai Mimari Hedef

```text
                     ┌──────────────────┐
                     │   Avalonia UI    │
                     └────────┬─────────┘
                              │
                     ┌────────▼─────────┐
                     │ Application Core │
                     └────────┬─────────┘
                              │
                     ┌────────▼─────────┐
                     │ Conversion Engine│
                     └───┬──────────┬───┘
                         │          │
                 ┌───────▼───┐  ┌──▼────────┐
                 │ Sims3     │  │ Sims4     │
                 │ Adapter   │  │ Adapter   │
                 └──────┬────┘  └────┬──────┘
                        │             │
                        └──────┬──────┘
                               │
                       ┌───────▼──────┐
                       │ Canonical    │
                       │ Asset Model  │
                       └───────┬──────┘
                               │
                   ┌───────────▼───────────┐
                   │ Mesh / Texture / Rig │
                   │ Transformation       │
                   └───────────┬───────────┘
                               │
                        ┌──────▼─────┐
                        │ Validation │
                        └────────────┘

                  Optional:
                        ┌────────────┐
                        │ AI Agent   │
                        │ Assistant  │
                        └────────────┘
```

---

# 51. Temel Ürün Kararı

Uygulamanın hedefi:

**“Her dosyayı sihirli biçimde convert eden araç”**

olmak yerine:

**“Desteklediği asset türlerinde güvenilir otomatik conversion yapan ve destekleyemediği noktayı açıkça gösteren conversion platformu”**

olmalıdır.

Bu yaklaşım özellikle Sims 3 ve Sims 4 arasındaki rig, UV, shader, metadata ve gameplay sistemi farklılıkları nedeniyle ürünün sürdürülebilirliği açısından kritik kabul edilir.
