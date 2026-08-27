# 

**Kurumsal SDLC Standardı**


## 

**Giriş**

Bu doküman, kurum içinde geliştirilen ve dış kaynak (Outsource) edilen web tabanlı uygulamaların **güvenlik (Security)**, **sürdürülebilirlik (Sustainability)** ve **operasyonel dayanıklılık (Operational Resilience)** gerekliliklerini tanımlar. Dokümanda yer alan **Zorunlu (Must)** maddeler, **canlıya alma (Go-Live)** için bağlayıcı kabul kriterleridir.

**Ne DEĞİLDİR ?** 

Aşağıdaki maddeler, bu dokümanın bilinçli olarak kapsamadığı veya amaçlamadığı alanları açıkça tanımlar.

**Bu Doküman Bir “Mükemmel Güvenlik” Garantisi Değildir**

Bu doküman:

* Tüm güvenlik risklerini sıfırlamayı  
* Her saldırıyı önlemeyi  
* Hiçbir zaman güvenlik olayı yaşanmamasını

vaat etmez.

Amaç; riskleri yok etmek değil, bilinçli şekilde yönetilebilir seviyeye indirmektir.

Bu dokümana uyum, “güvenli” olmayı değil; kabul edilebilir risk duruşunu (acceptable risk posture) sağlar.

**Bu Doküman Bir Ürün veya Teknoloji Rehberi Değildir**

Bu doküman:

* Belirli bir programlama dili  
* Belirli bir framework  
* Belirli bir güvenlik ürünü veya vendor  
* Belirli bir bulut sağlayıcı

dayatmaz. Nasıl yapılacağına değil, hangi güvenlik çıktılarının sağlanması gerektiğine odaklanır. Tedarikçi veya ekipler kendi araçlarını, teknolojilerini ve yöntemlerini kullanabilir; ancak ortaya çıkan sonuçlar bu dokümanda tanımlanan gerekliliklerle uyumlu olmak zorundadır.

**Bu Doküman Bir Uyum (Compliance) Kutucuğu Değildir**

Bu doküman:

* “Doküman var mı?” sorusunu geçmek için  
* Sadece denetimde gösterilecek bir evrak olarak  
* Okunmadan imzalanmak üzere

hazırlanmamıştır. Burada yer alan kontrollerin amacı:

* Gerçek hayatta uygulanabilir olmak  
* Canlıya alma kararını kanıta dayalı hale getirmek  
* Güvenliği “sonradan eklenen” bir yük olmaktan çıkarmaktır

**Bu Doküman Güvenliği Sadece Test Aşamasına Bırakmaz**

Bu doküman:

* Güvenliği yalnızca sızma testine  
* Sadece otomatik tarama araçlarına  
* Projenin en sonuna  
* bırakan bir yaklaşımı reddeder.

Güvenlik; planlama, tasarım, geliştirme, test, dağıtım ve operasyonun tamamına yayılmış bir sorumluluktur. Bu nedenle Secure SDLC, tek bir aşama değil; bütünsel bir yaklaşımdır.

**Bu Doküman “Bizim Sistemimiz Özel” İstisnasını Kabul Etmez**

Bu doküman:

* “Bizim sistemimiz dahili”  
* “Bizim kullanıcılarımız bilinçli”  
* “Bizim verimiz kritik değil”  
* “Bizim mimarimiz farklı”

gibi gerekçeleri tek başına geçerli istisna olarak kabul etmez. İstisnalar:

* Yalnızca yazılı risk kabulü (risk acceptance)  
* Etki, olasılık ve sorumlusu tanımlanmış  
* Süreli ve izlenebilir

olduğu sürece mümkündür.

**Bu Doküman Güvenliği Tek Bir Rolün Sorumluluğu Olarak Görmez**

Bu doküman:

* Güvenliği sadece ICT’nin  
* Sadece güvenlik ekibinin  
* Sadece test ekibinin

sorumluluğu olarak tanımlamaz. Güvenlik;

* Ürün sahibi  
* Geliştirme ekibi  
* Test  
* Operasyon  
* Tedarikçi

dahil olmak üzere tüm paydaşların ortak sorumluluğudur.

**Bu Doküman “Bir Kez Yap, Bitir” Yaklaşımını Benimsemez**

Bu doküman:

* Bir kez uyulup rafa kaldırılacak  
* Proje bitince unutulacak  
* Değişmeyen, yaşayan olmayan

bir metin değildir. Tehditler, teknolojiler ve iş ihtiyaçları değiştikçe; bu doküman da gözden geçirilmek, güncellenmek ve evrilmek zorundadır.

**Son Not**

Bu doküman:

* Güvenliği zorlaştırmak için değil  
* Geliştirmeyi yavaşlatmak için değil  
* Sorumluluk dağıtmak için değil  
* Bilinçli karar almayı kolaylaştırmak için vardır.

Bu dokümana uyum, “sıfır risk” değil; kontrol altında, bilinen ve yönetilen risk anlamına gelir.

---

## 

## 

[1\. Amaç ve Kapsam	7](#1.-amaç-ve-kapsam)

[1.1. Amaç	7](#1.1.-amaç)

[1.1.1. Bu dokümanın amacı	7](#1.1.1.-bu-dokümanın-amacı)

[1.1.2. Hedef çıktı	7](#1.1.2.-hedef-çıktı)

[1.2. Kapsam	7](#1.2.-kapsam)

[1.2.1. Dahil olanlar	7](#1.2.1.-dahil-olanlar)

[1.2.2. Hariç olanlar	7](#1.2.2.-hariç-olanlar)

[2\. Yönetişim (Governance) ve Sorumluluklar	8](#2.-yönetişim-\(governance\)-ve-sorumluluklar)

[2.1. RACI (Minimum)	8](#2.1.-raci-\(minimum\))

[2.1.1. Ürün Sahibi / İş Sahibi (Product Owner / Business Owner)	8](#2.1.1.-ürün-sahibi-/-i̇ş-sahibi-\(product-owner-/-business-owner\))

[2.1.2. ICT Güvenlik ve Operasyon (ICT \- Security & Operations)	8](#2.1.2.-ict-güvenlik-ve-operasyon-\(ict---security-&-operations\))

[2.1.3. Geliştirme Ekibi / Tedarikçi (Development Team / Vendor)	8](#2.1.3.-geliştirme-ekibi-/-tedarikçi-\(development-team-/-vendor\))

[2.1.4. QA / Test	8](#2.1.4.-qa-/-test)

[3\. Güvenli Yazılım Geliştirme Yaşam Döngüsü Kuralları	8](#3.-güvenli-yazılım-geliştirme-yaşam-döngüsü-kuralları)

[3.1. Planlama (Planning) ve Gereksinimler (Requirements)	8](#3.1.-planlama-\(planning\)-ve-gereksinimler-\(requirements\))

[3.1.1. Tehdit Modellemesi (Threat Modeling) (Zorunlu)	8](#3.1.1.-tehdit-modellemesi-\(threat-modeling\)-\(zorunlu\))

[3.1.2. Güvenlik Gereksinimleri (Security Requirements)	9](#3.1.2.-güvenlik-gereksinimleri-\(security-requirements\))

[3.2. Güvenli Kodlama Standardı (Secure Coding Standard)	10](#3.2.-güvenli-kodlama-standardı-\(secure-coding-standard\))

[3.2.1. OWASP Uyum (OWASP Alignment) (Zorunlu)	10](#3.2.1.-owasp-uyum-\(owasp-alignment\)-\(zorunlu\))

[3.2.2. Güvenli Kodlama Prensipleri (Secure Coding Principles)	10](#3.2.2.-güvenli-kodlama-prensipleri-\(secure-coding-principles\))

[3.3. Kod Gözden Geçirme (Code Review)	11](#3.3.-kod-gözden-geçirme-\(code-review\))

[3.3.1. Çekme İsteği Politikası (Pull Request Policy) (Zorunlu)	11](#3.3.1.-çekme-i̇steği-politikası-\(pull-request-policy\)-\(zorunlu\))

[3.3.2. Gözden Geçirme Nasıl Yapılmalı	11](#3.3.2.-gözden-geçirme-nasıl-yapılmalı)

[3.3.3. Güvenlik Odaklı Gözden Geçirme Kontrol Listesi (Security-Focused Review Checklist)	11](#3.3.3.-güvenlik-odaklı-gözden-geçirme-kontrol-listesi-\(security-focused-review-checklist\))

[3.4. Bağımlılık (Dependency) ve Tedarik Zinciri Güvenliği (Supply Chain Security)	12](#3.4.-bağımlılık-\(dependency\)-ve-tedarik-zinciri-güvenliği-\(supply-chain-security\))

[3.4.1. Bağımlılık Taraması (Dependency Scanning) (Zorunlu)	12](#3.4.1.-bağımlılık-taraması-\(dependency-scanning\)-\(zorunlu\))

[3.4.2. Güncelleme ve Risk Yönetimi	12](#3.4.2.-güncelleme-ve-risk-yönetimi)

[3.5. Gizli Bilgi Yönetimi (Secrets Management)	13](#3.5.-gizli-bilgi-yönetimi-\(secrets-management\))

[3.5.1. Yasaklar (Zorunlu)	13](#3.5.1.-yasaklar-\(zorunlu\))

[3.5.2. Doğru Yaklaşım	13](#3.5.2.-doğru-yaklaşım)

[4\. Uygulama Güvenliği (Application Security) Gereklilikleri	14](#4.-uygulama-güvenliği-\(application-security\)-gereklilikleri)

[4.1. Kimlik Doğrulama (Authentication)	14](#4.1.-kimlik-doğrulama-\(authentication\))

[4.1.1. Kimlik Doğrulama Neden Kritiktir?	14](#4.1.1.-kimlik-doğrulama-neden-kritiktir?)

[4.1.2. Standartlar (Zorunlu)	14](#4.1.2.-standartlar-\(zorunlu\))

[4.1.3. Oturum Yönetimi (Session Management)	14](#4.1.3.-oturum-yönetimi-\(session-management\))

[4.1.4. Kaba Kuvvet (Brute Force) ve Kimlik Bilgisi İstismarı (Credential Abuse) Koruması	15](#4.1.4.-kaba-kuvvet-\(brute-force\)-ve-kimlik-bilgisi-i̇stismarı-\(credential-abuse\)-koruması)

[4.2. Yetkilendirme (Authorization)	15](#4.2.-yetkilendirme-\(authorization\))

[4.2.1. En Az Ayrıcalık (Least Privilege) (Zorunlu)	15](#4.2.1.-en-az-ayrıcalık-\(least-privilege\)-\(zorunlu\))

[4.2.2. Nesne Seviyesinde Yetkilendirme (Object‑Level Authorization) / IDOR	16](#4.2.2.-nesne-seviyesinde-yetkilendirme-\(object‑level-authorization\)-/-idor)

[4.3. Girdi Doğrulama (Input Validation) ve Çıktı Kodlama (Output Encoding)	16](#4.3.-girdi-doğrulama-\(input-validation\)-ve-çıktı-kodlama-\(output-encoding\))

[4.3.1. Girdi Doğrulama Neden Arka Uçta (Backend) Olmalı?	17](#4.3.1.-girdi-doğrulama-neden-arka-uçta-\(backend\)-olmalı?)

[4.3.2. Nasıl Uygulanır (Somut Anlatım)	17](#4.3.2.-nasıl-uygulanır-\(somut-anlatım\))

[4.3.3. Çıktı Kodlama (Output Encoding)	17](#4.3.3.-çıktı-kodlama-\(output-encoding\))

[4.4. Loglama (Logging) ve Denetlenebilirlik (Auditability)	17](#4.4.-loglama-\(logging\)-ve-denetlenebilirlik-\(auditability\))

[4.4.1. Loglama Neden Güvenlik Konusudur?	18](#4.4.1.-loglama-neden-güvenlik-konusudur?)

[4.4.2. Ne Loglanmalı? (Somut Liste)	18](#4.4.2.-ne-loglanmalı?-\(somut-liste\))

[4.4.3. Log Formatı ve İçeriği	18](#4.4.3.-log-formatı-ve-i̇çeriği)

[4.4.4. Denetim Kaydı (Audit Trail)	18](#4.4.4.-denetim-kaydı-\(audit-trail\))

[4.5. Dosya Yükleme (File Upload) ve İçerik İşleme (Content Handling)	19](#4.5.-dosya-yükleme-\(file-upload\)-ve-i̇çerik-i̇şleme-\(content-handling\))

[4.5.1. Dosya Yükleme Neden Risklidir?	19](#4.5.1.-dosya-yükleme-neden-risklidir?)

[4.5.2. Nasıl Güvenli Yapılır?	19](#4.5.2.-nasıl-güvenli-yapılır?)

[4.5.3. Depolama (Storage) ve Erişim	19](#4.5.3.-depolama-\(storage\)-ve-erişim)

[4.6. İş Mantığı İstismarı (Business Logic Abuse)	20](#4.6.-i̇ş-mantığı-i̇stismarı-\(business-logic-abuse\))

[4.7 Zero Trust ve Mikro Segmentasyon Yaklaşımı	21](#4.7-zero-trust-ve-mikro-segmentasyon-yaklaşımı)

[4.8 API Güvenliği (API Security)	21](#4.8-api-güvenliği-\(api-security\))

[5\. DevOps ve CI/CD Gereklilikleri	22](#5.-devops-ve-ci/cd-gereklilikleri)

[5.1. CI/CD Neden Güvenlik Konusudur?	22](#5.1.-ci/cd-neden-güvenlik-konusudur?)

[5.2. CI Pipeline Minimum Güvenlik Kontrolleri (Zorunlu)	23](#5.2.-ci-pipeline-minimum-güvenlik-kontrolleri-\(zorunlu\))

[5.2.1. SAST (Static Application Security Testing)	23](#5.2.1.-sast-\(static-application-security-testing\))

[5.2.2. SCA (Software Composition Analysis)	23](#5.2.2.-sca-\(software-composition-analysis\))

[5.2.3. Gizli Bilgi Taraması (Secret Scanning)	24](#5.2.3.-gizli-bilgi-taraması-\(secret-scanning\))

[5.3. DAST (Dynamic Application Security Testing)	24](#5.3.-dast-\(dynamic-application-security-testing\))

[5.4. Boru Hattı (Pipeline) Yeşilken Neden Risk Kalır?	24](#5.4.-boru-hattı-\(pipeline\)-yeşilken-neden-risk-kalır?)

[5.5. Sürekli Dağıtım (CD), Sürüm (Release) ve Geri Alma (Rollback)	25](#5.5.-sürekli-dağıtım-\(cd\),-sürüm-\(release\)-ve-geri-alma-\(rollback\))

[5.5.1. Sürüm (Release) Neden Güvenlik Konusudur?	25](#5.5.1.-sürüm-\(release\)-neden-güvenlik-konusudur?)

[5.5.2. Geri Alma Stratejisi (Rollback Strategy) (Zorunlu \- Must)	25](#5.5.2.-geri-alma-stratejisi-\(rollback-strategy\)-\(zorunlu---must\))

[5.6. Kod Olarak Altyapı (Infrastructure as Code) (IaC)	25](#5.6.-kod-olarak-altyapı-\(infrastructure-as-code\)-\(iac\))

[5.7. Erişim Kontrolü (Access Control) ve Boru Hattı Güvenliği (Pipeline Security)	26](#5.7.-erişim-kontrolü-\(access-control\)-ve-boru-hattı-güvenliği-\(pipeline-security\))

[5.8. Container ve Serverless Güvenliği	26](#5.8.-container-ve-serverless-güvenliği)

[6\. Test, Operasyon ve Canlıya Alma	26](#6.-test,-operasyon-ve-canlıya-alma)

[6.1. Güvenlik Testi (Security Testing) Neden Ayrı Bir Konudur?	27](#6.1.-güvenlik-testi-\(security-testing\)-neden-ayrı-bir-konudur?)

[6.2. Test Türleri ve Amaçları	27](#6.2.-test-türleri-ve-amaçları)

[6.2.1. Birim (Unit) ve Entegrasyon (Integration) Testler	27](#6.2.1.-birim-\(unit\)-ve-entegrasyon-\(integration\)-testler)

[6.2.2. DAST (Sürüm Öncesi \- Release Öncesi)	27](#6.2.2.-dast-\(sürüm-öncesi---release-öncesi\))

[6.2.3. Sızma Testi (Penetration Test)	28](#6.2.3.-sızma-testi-\(penetration-test\))

[6.3. Operasyonel Hazırlık	28](#6.3.-operasyonel-hazırlık)

[6.3.1. İzleme (Monitoring) Neden Sadece Uptime Değildir?	28](#6.3.1.-i̇zleme-\(monitoring\)-neden-sadece-uptime-değildir?)

[6.3.2. Olay Yönetimi (Incident Management)	29](#6.3.2.-olay-yönetimi-\(incident-management\))

[6.3.3. Olay Sonrası Değerlendirme (Postmortem) Kültürü	29](#6.3.3.-olay-sonrası-değerlendirme-\(postmortem\)-kültürü)

[6.3.4. Threat Intelligence ve Proaktif İzleme (Operasyonel ve SOC Perspektifi)	30](#6.3.4.-threat-intelligence-ve-proaktif-i̇zleme-\(operasyonel-ve-soc-perspektifi\))

[6.4. Yedekleme (Backup) ve Geri Yükleme (Recovery)	30](#6.4.-yedekleme-\(backup\)-ve-geri-yükleme-\(recovery\))

[6.5. Canlıya Alma Kapısı (Go-Live Gate) (En Kritik Nokta)	30](#6.5.-canlıya-alma-kapısı-\(go-live-gate\)-\(en-kritik-nokta\))

[6.5.1. Canlıya Alma Öncesi Zorunlu Kontroller (Zorunlu \- Must)	30](#6.5.1.-canlıya-alma-öncesi-zorunlu-kontroller-\(zorunlu---must\))

[6.5.2. Risk kabulü (Risk Acceptance)	31](#6.5.2.-risk-kabulü-\(risk-acceptance\))

[6.6. Canlıya Alma Sonrası İlk 30 Gün	31](#6.6.-canlıya-alma-sonrası-i̇lk-30-gün)

[7\. Canlıya Alma Güvenlik Kontrol Listesi (Go-Live Security Checklist) (Tek Sayfa – İmzalı)	31](#7.-canlıya-alma-güvenlik-kontrol-listesi-\(go-live-security-checklist\)-\(tek-sayfa-–-i̇mzalı\))

[7.1. Proje Bilgileri	32](#7.1.-proje-bilgileri)

[7.2. Secure SDLC Kontrolleri	32](#7.2.-secure-sdlc-kontrolleri)

[7.3. Uygulama Güvenliği (Application Security) Kontrolleri	32](#7.3.-uygulama-güvenliği-\(application-security\)-kontrolleri)

[7.4. DevOps ve CI/CD Kontrolleri	33](#7.4.-devops-ve-ci/cd-kontrolleri)

[7.5. Test ve Operasyonel Hazırlık	34](#7.5.-test-ve-operasyonel-hazırlık)

[7.6. Açık Riskler ve Risk kabulü (Risk Acceptance)	34](#7.6.-açık-riskler-ve-risk-kabulü-\(risk-acceptance\))

[7.7. Canlıya Alma Onayı	35](#7.7.-canlıya-alma-onayı)

[8\. Tedarikçi Güvenlik Eki (Vendor Security Annex) (Sözleşme Eki)	35](#8.-tedarikçi-güvenlik-eki-\(vendor-security-annex\)-\(sözleşme-eki\))

[8.1. Genel Yükümlülükler	36](#8.1.-genel-yükümlülükler)

[8.1.1. Kurumsal SDLC Zorunluluğu	36](#8.1.1.-Kurumsal-sdlc-zorunluluğu)

[8.2. Güvenlik Gereklilikleri ve Teslimatlar (Deliverables)	36](#8.2.-güvenlik-gereklilikleri-ve-teslimatlar-\(deliverables\))

[8.2.1. Zorunlu Teslimat Listesi (Deliverable List)	36](#8.2.1.-zorunlu-teslimat-listesi-\(deliverable-list\))

[8.3. Zafiyet Yönetimi (Vulnerability Management) ve SLA	36](#8.3.-zafiyet-yönetimi-\(vulnerability-management\)-ve-sla)

[8.3.1. Şiddet (Severity) Tanımları	36](#8.3.1.-şiddet-\(severity\)-tanımları)

[8.3.2. Düzeltme SLA (Fix SLA) (Örnek)	36](#8.3.2.-düzeltme-sla-\(fix-sla\)-\(örnek\))

[8.4. Canlıya Alma ve Ödeme Bağı	37](#8.4.-canlıya-alma-ve-ödeme-bağı)

[8.4.1. Canlıya Alma Şartı	37](#8.4.1.-canlıya-alma-şartı)

[8.4.2. Ödeme Koşulu	37](#8.4.2.-ödeme-koşulu)

[8.5. Erişim ve Hesap Yönetimi	37](#8.5.-erişim-ve-hesap-yönetimi)

[Nasıl uygulanır:	38](#nasıl-uygulanır:)

[Yeterli kabul edilen seviye:	38](#yeterli-kabul-edilen-seviye:)

[8.6. Gizlilik (Confidentiality) ve Veri Koruma (Data Protection)	38](#8.6.-gizlilik-\(confidentiality\)-ve-veri-koruma-\(data-protection\))

[8.7. Denetim ve Doğrulama Hakkı	38](#8.7.-denetim-ve-doğrulama-hakkı)

[8.8. İhlal Bildirimi (Breach Notification)	38](#8.8.-i̇hlal-bildirimi-\(breach-notification\))

[8.9. Sözleşmesel Yaptırımlar	39](#8.9.-sözleşmesel-yaptırımlar)

[9\. Gap Analysis Template (Mevcut Sistemler İçin)	39](#9.-gap-analysis-template-\(mevcut-sistemler-i̇çin\))

[9.3. Secure SDLC Gap Analizi	39](#9.3.-secure-sdlc-gap-analizi)

[9.4. Application Security Gap Analizi	40](#9.4.-application-security-gap-analizi)

[9.5. DevOps & CI/CD Gap Analizi	41](#9.5.-devops-&-ci/cd-gap-analizi)

[9.6. Test ve Operasyon Gap Analizi	41](#9.6.-test-ve-operasyon-gap-analizi)

[9.7. Risk Özeti ve Yol Haritası	42](#9.7.-risk-özeti-ve-yol-haritası)

[9.7.1. Kritik Riskler (Öncelik 1\)	42](#9.7.1.-kritik-riskler-\(öncelik-1\))

[9.7.2. Orta Vadeli İyileştirmeler	42](#9.7.2.-orta-vadeli-i̇yileştirmeler)

[9.7.3. Kabul Edilen Riskler	43](#9.7.3.-kabul-edilen-riskler)

[10\. Gözden Geçirme, Versiyonlama ve Süreklilik	43](#10.-gözden-geçirme,-versiyonlama-ve-süreklilik)

[10.1. Gözden Geçirme Cycle	43](#10.1.-gözden-geçirme-cycle)

[10.2. Süreklilik	43](#10.2.-süreklilik)

[11\. Canlıya Alma Öncesi Uyum (Compliance) Checklist	43](#11.-canlıya-alma-öncesi-uyum-\(compliance\)-checklist)

[11.1. Uyum Checklist Tablosu	44](#11.1.-uyum-checklist-tablosu)

[11.2. Uyum Değerlendirme ve Onay	46](#11.2.-uyum-değerlendirme-ve-onay)

## 

**Kurumsal SDLC Standardı**

Bu doküman, kurum içinde geliştirilen ve/veya dış kaynak (outsource) kullanılarak geliştirilen uygulamaların **güvenlik (security)**, **sürdürülebilirlik (sustainability)** ve **operasyonel dayanıklılık (operational resilience)** gerekliliklerini tanımlar. Dokümanda yer alan **Zorunlu (Must)** maddeler, **canlıya alma (go-live)** için bağlayıcı kabul kriterleridir.

---

## **1\. Amaç ve Kapsam** {#1.-amaç-ve-kapsam}

### **1.1. Amaç** {#1.1.-amaç}

#### **1.1.1. Bu dokümanın amacı** {#1.1.1.-bu-dokümanın-amacı}

Bu dokümanın amacı; Kurumsaliçin geliştirilen uygulamaların geliştirme, test, dağıtım (deploy) ve operasyon süreçlerinde uygulanacak **minimum güvenlik (security) ve operasyonel standartları** tanımlamak ve bu standartlara uyum sağlanması halinde ürünün **canlı ortama alınabilir** olduğunu garanti altına almaktır.

#### **1.1.2. Hedef çıktı** {#1.1.2.-hedef-çıktı}

Bu şartlara uyularak geliştirilmiş bir ürün:

* Kabul edilebilir bir **risk duruşu (risk posture)** ile canlıya alınır  
* Operasyon sırasında izlenebilir (**gözlemlenebilirlik (observability)**) ve yönetilebilir olur  
* Uzun vadede **bakımı yapılabilir (maintainable)** ve **ölçeklenebilir (scalable)** kalır

### **1.2. Kapsam** {#1.2.-kapsam}

#### **1.2.1. Dahil olanlar** {#1.2.1.-dahil-olanlar}

* Uygulama (Masaüstü (desktop), ön yüz (frontend), arka uç (backend))  
* API (Application Programming Interface) ve entegrasyon servisleri (integration services)  
* CI/CD pipeline ve altyapı (infrastructure)  
* Bulut (cloud) ve yerinde (on‑prem) dağıtım (deployment) ortamları  
* Üçüncü taraf (third‑party) kütüphaneler (libraries) ve SaaS (Software as a Service) entegrasyonları (integrations)

#### **1.2.2. Hariç olanlar** {#1.2.2.-hariç-olanlar}

* Fiziksel güvenlik (physical security)  
* Son kullanıcı cihaz yönetimi (end‑user device management) (MDM vb.)

---

## **2\. Yönetişim (Governance) ve Sorumluluklar** {#2.-yönetişim-(governance)-ve-sorumluluklar}

### **2.1. RACI (Minimum)** {#2.1.-raci-(minimum)}

#### **2.1.1. Ürün Sahibi / İş Sahibi (Product Owner / Business Owner)** {#2.1.1.-ürün-sahibi-/-i̇ş-sahibi-(product-owner-/-business-owner)}

* İş kapsamı (business scope) ve veri sınıflandırması (data classification) tanımlar  
* Düzenleyici (regulatory) ve uyum (compliance) gereksinimlerini belirtir

#### **2.1.2. ICT Güvenlik ve Operasyon (ICT \- Security & Operations)** {#2.1.2.-ict-güvenlik-ve-operasyon-(ict---security-&-operations)}

* Güvenlik gereksinimleri (security requirements) ve politika (policy)’leri belirler  
* Risk değerlendirmesi (risk assessment) ve canlıya alma kapısı (go‑live gate) onayını verir

#### **2.1.3. Geliştirme Ekibi / Tedarikçi (Development Team / Vendor)** {#2.1.3.-geliştirme-ekibi-/-tedarikçi-(development-team-/-vendor)}

* Güvenli Yazılım Geliştirme Yaşam Döngüsü (Secure SDLC) uygulamakla yükümlüdür  
* Tüm güvenlik (security) ve test çıktılarının **kanıt (evidence)**’ını sağlar

#### **2.1.4. QA / Test** {#2.1.4.-qa-/-test}

* Test stratejisi (test strategy) ve test yürütümü (test execution) koordinasyonu  
* Güvenlik testi (security test) sonuçlarının takibi

---

## **3\. Güvenli Yazılım Geliştirme Yaşam Döngüsü Kuralları** {#3.-güvenli-yazılım-geliştirme-yaşam-döngüsü-kuralları}

### **3.1. Planlama (Planning) ve Gereksinimler (Requirements)** {#3.1.-planlama-(planning)-ve-gereksinimler-(requirements)}

#### **3.1.1. Tehdit Modellemesi (Threat Modeling) (Zorunlu)** {#3.1.1.-tehdit-modellemesi-(threat-modeling)-(zorunlu)}

Tehdit modellemesi (threat modeling), geliştirilen uygulamanın **nasıl saldırıya uğrayabileceğini sistematik olarak düşünme egzersizidir**. Amaç mükemmel bir analiz yapmak değil, en bariz ve yüksek etkili riskleri daha kod yazılmadan görünür hale getirmektir.

Bu çalışma, güvenlik uzmanı gerektiren akademik bir aktivite olarak değil; geliştirici ve ürün ekiplerinin **yapıyı gerçekten anladığını kanıtlayan** bir çıktı olarak görülmelidir.

**Nasıl yapılır:**

1. Uygulamanın basit bir mimari resmi (architecture diagram) çizilir. Bu çizim; ön yüz (frontend), arka uç (backend), veritabanı (database) ve dış servisleri (external services) kutular halinde gösterecek kadar basit olabilir.  
2. Bu kutular arasında hangi verinin aktığı **veri akışı (data flow)** oklarıyla gösterilir (örnek: kullanıcıdan API’ye giriş (login) bilgisi gider, API veritabanından veri çeker vb.).  
3. Her bir ok ve bileşen için şu soru sorulur: “Burada biri kötü niyetli davranırsa ne olabilir?”  
4. Bu düşünce STRIDE başlıkları altında gruplanır:  
   * **Spoofing:** Bir kullanıcı başka biri gibi davranabilir mi?  
   * **Tampering:** Gönderilen veri yolda değiştirilebilir mi?  
   * **Repudiation:** Yapılan işlemler inkâr edilebilir mi?  
   * **Information Disclosure:** Hassas veri sızabilir mi?  
   * **Denial of Service:** Sistem çalışamaz hale getirilebilir mi?  
   * **Elevation of Privilege:** Yetkisi olmayan biri admin olabilir mi?

**Çıktı nasıl olmalı (yeterli kabul edilen seviye):**

* En az 1 sayfalık bir tablo  
* Her satırda:  
  * İlgili bileşen veya veri akışı (data flow)  
  * Olası tehdit  
  * O tehdidin etkisi (kısa cümle)  
  * Alınacak önlem (örnek: hız sınırlama (rate limiting), yetkilendirme kontrolü (authorization check), şifreleme (encryption))

Bu dokümanın amacı “her şeyi düşündük” demek değil, **hiç düşünülmemiş bariz risklerin olmadığını göstermek**tir.

---

#### **3.1.2. Güvenlik Gereksinimleri (Security Requirements)** {#3.1.2.-güvenlik-gereksinimleri-(security-requirements)}

Güvenlik gereksinimleri (security requirements), güvenliği soyut bir kavram olmaktan çıkarıp **yapılması gereken iş** haline getirir. Yazılı olmayan güvenlik beklentileri, pratikte yok hükmündedir.

**Neden gereklidir:**

* Güvenlik konuları genellikle “zaman kalırsa” yapılır  
* İş listesi (backlog)’da yer almayan işlerin teslim edilme ihtimali düşüktür

**Nasıl yazılır:**

* Tehdit modellemesi (threat modeling) çıktısındaki her kritik risk için en az bir güvenlik gereksinimi tanımlanır  
* Bu gereksinimler; kullanıcı hikayesi (user story) veya kabul kriteri (acceptance criteria) formatında iş listesi (backlog)’a eklenir

**Somut örnekler:**

* "Sistem olarak, rol tabanlı bir yetkilendirmeyi admin API lerine uygulanmalı"  
* "Sistem olarak, tüm kimlik doğrulama denemeleri loglanmalı"  
* "Sistem olarak, açık API lara mutlaka hız sınırı uygulanmalı"

**Yaygın yanlışlar:**

* Güvenliği sadece test aşamasına bırakmak  
* “Bu zaten framework’te var” varsayımı

**Yeterli kabul edilen seviye:**

* Güvenlik gereksinimleri sprint planlarında görünür olmalı  
* Tamamlanmadan ilgili özellik (feature) “tamamlandı (done)” sayılmamalı

---

### **3.2. Güvenli Kodlama Standardı (Secure Coding Standard)** {#3.2.-güvenli-kodlama-standardı-(secure-coding-standard)}

Güvenli kodlama (secure coding), geliştiricinin kişisel tecrübesine bırakılabilecek bir konu değildir. Amaç, ekipte kim kod yazarsa yazsın **aynı minimum güvenlik seviyesinin** korunmasını sağlamaktır.

#### **3.2.1. OWASP Uyum (OWASP Alignment) (Zorunlu)** {#3.2.1.-owasp-uyum-(owasp-alignment)-(zorunlu)}

Bu dokümanda geçen güvenli kodlama (secure coding) beklentileri, **OWASP Top 10** ve **OWASP ASVS Level 2** ile uyumlu olacak şekilde belirlenmiştir. Ekiplerin bu dokümanları ezberlemesi beklenmez; ancak burada tanımlanan kurallara uyması zorunludur.

#### **3.2.2. Güvenli Kodlama Prensipleri (Secure Coding Principles)** {#3.2.2.-güvenli-kodlama-prensipleri-(secure-coding-principles)}

Bu prensipler “ideal dünya” için değil, **günlük geliştirme pratiği** için yazılmıştır.

**Neden gereklidir:**

* Güvenlik açıklarının büyük kısmı karmaşık saldırılardan değil, basit programlama hatalarından doğar  
* Aynı hatalar farklı projelerde tekrar tekrar yapılır

**Nasıl uygulanır:**

* Kullanıcıdan gelen her veri varsayılan olarak güvensiz kabul edilir  
* Bu veriler işlenmeden önce doğrulanır (format, uzunluk, izinli liste (allow‑list))  
* Veriler başka bir yere gönderilmeden önce bağlama uygun kodlanır (encode)  
* Hata oluştuğunda sistem iç detaylarını ifşa etmez

**Yaygın yanlışlar:**

* “Ön yüz (frontend) kontrol ediyor, arka uç (backend)’a gerek yok”  
* “Bu dahili (internal) API, dışarıdan erişilmiyor”  
* “Exception zaten sadece biz görüyoruz”

**Yeterli kabul edilen seviye:**

* Ekip içinde yazılı veya referans verilen bir güvenli kodlama standardı (secure coding standard) mevcut olmalı  
* Kod gözden geçirme (code review) sırasında bu maddeler gerçekten kontrol edilmeli

---

### **3.3. Kod Gözden Geçirme (Code Review)** {#3.3.-kod-gözden-geçirme-(code-review)}

Kod gözden geçirme (code review), hataları yakalamaktan çok **yanlış varsayımları erken aşamada durdurmak** için yapılır. Güvenlik açısından bakıldığında, kod gözden geçirme (code review) otomatik araçların yakalayamadığı iş mantığı (business logic) hatalarını yakalamanın en etkili yoludur.

#### **3.3.1. Çekme İsteği Politikası (Pull Request Policy) (Zorunlu)** {#3.3.1.-çekme-i̇steği-politikası-(pull-request-policy)-(zorunlu)}

**Neden gereklidir:**

* Tek kişinin yazdığı ve onayladığı kod en yüksek riskli koddur  
* Hatalar genellikle “çalışıyor” olduğu için fark edilmez

**Nasıl uygulanır:**

* Tüm kod değişiklikleri çekme isteği (pull request) üzerinden yapılır  
* Doğrudan main/master dal (branch)’a commit atılamaz  
* En az 1 inceleyici (reviewer) zorunludur (kritik modüller için 2\)

**Yeterli kabul edilen seviye:**

* Korumalı dal (protected branch) aktif olmalı  
* Gözden geçirme (review) yapılmadan birleştirme (merge) teknik olarak mümkün olmamalı

#### **3.3.2. Gözden Geçirme Nasıl Yapılmalı** {#3.3.2.-gözden-geçirme-nasıl-yapılmalı}

Kod gözden geçirme (code review) sırasında sadece “syntax doğru mu” bakılmaz. Aşağıdaki sorular bilinçli şekilde sorulmalıdır:

* Bu kod gerçekten doğru kullanıcıyı mı yetkilendiriyor? (yetkilendirme (authorization))  
* Başka bir kullanıcı bu endpoint’i çağırabilir mi?  
* Kullanıcıdan gelen veri doğrudan kullanılıyor mu?  
* Hata durumunda sistem iç detaylarını açığa çıkarıyor mu?  
* Log’lar olası bir olayı incelemek için yeterli mi?

**Yaygın yanlışlar:**

* Sadece biçimlendirme (formatting) ve isimlendirme (naming) kontrol etmek  
* “Bu zaten eski kod, dokunmayalım” demek  
* Gözden geçirmeyi formalite olarak görmek

#### **3.3.3. Güvenlik Odaklı Gözden Geçirme Kontrol Listesi (Security-Focused Review Checklist)** {#3.3.3.-güvenlik-odaklı-gözden-geçirme-kontrol-listesi-(security-focused-review-checklist)}

Minimum olarak aşağıdakiler kontrol edilmelidir:

* Kimlik doğrulama (authentication) ve yetkilendirme (authorization) kontrolleri mevcut mu?  
* Girdi doğrulama (input validation) arka uç (backend) tarafında yapılıyor mu?  
* Gizli bilgiler (secrets) veya kimlik bilgileri (credentials) koda sızmış mı?  
* Loglama (logging) ve hata yönetimi (error handling) yeterli mi?

**Yeterli kabul edilen seviye:**

* Gözden geçirme yorumları anlamlı sorular içeriyor olmalı  
* Aynı tip hatalar tekrar tekrar geçmemeli

---

### **3.4. Bağımlılık (Dependency) ve Tedarik Zinciri Güvenliği (Supply Chain Security)** {#3.4.-bağımlılık-(dependency)-ve-tedarik-zinciri-güvenliği-(supply-chain-security)}

Modern uygulamalar kendi yazdığımız koddan çok, dışarıdan aldığımız kodlara dayanır. Bu nedenle tedarik zinciri güvenliği (supply chain security) göz ardı edildiğinde, ekip farkında olmadan yüksek riskli bir ürünü canlıya alabilir.

#### **3.4.1. Bağımlılık Taraması (Dependency Scanning) (Zorunlu)** {#3.4.1.-bağımlılık-taraması-(dependency-scanning)-(zorunlu)}

**Neden gereklidir:**

* Bilinen açıklar (known vulnerabilities) çoğu zaman aylarca düzeltme (fix) edilmeden sistemlerde kalır  
* Saldırganlar bu açıkları aktif olarak tarar

**Nasıl uygulanır:**

* CI pipeline içinde otomatik tarama çalıştırılır  
* Tüm bağımlılıklar (dependencies) bilinen CVE’ler açısından kontrol edilir

**Yaygın yanlışlar:**

* “Bu kütüphaneyi biz yazmadık”  
* “Güncellersek (upgrade) bir şey bozulabilir”

**Yeterli kabul edilen seviye:**

* Kritik (Critical) zafiyet (vulnerability) varken canlıya alma (go‑live) yapılmaz  
* Yüksek (High) zafiyet için düzeltme (fix) veya yazılı risk kabulü (risk acceptance) gerekir

#### **3.4.2. Güncelleme ve Risk Yönetimi** {#3.4.2.-güncelleme-ve-risk-yönetimi}

Bağımlılık güncellemesi (dependency upgrade) bir opsiyon değil, **sürekli bir sorumluluktur**.

**Nasıl yönetilir:**

* Düzenli güncelleme penceresi (upgrade window) tanımlanır  
* Büyük güncellemeler (major upgrades) planlı şekilde yapılır

**Yeterli kabul edilen seviye:**

* Bağımlılıkların güncel tutulduğunu gösteren pipeline raporları bulunur

---

### **3.5. Gizli Bilgi Yönetimi (Secrets Management)** {#3.5.-gizli-bilgi-yönetimi-(secrets-management)}

Gizli bilgi yönetimi (secrets management), en küçük hatanın en büyük etkiye sahip olduğu konudur. Bir gizli bilginin (secret) sızması, çoğu zaman **tüm sistemin ele geçirilmesi** anlamına gelir.

#### **3.5.1. Yasaklar (Zorunlu)** {#3.5.1.-yasaklar-(zorunlu)}

* Kaynak kod (source code) içinde parola (password), API anahtarı (API key), token bulunamaz  
* Git geçmişi (git history) içinde kalıcı gizli bilgi (secret) bırakılması kabul edilemez

**Neden:**

* Depo (repository) erişimi olan herkes bu bilgilere ulaşabilir  
* Bu bilgiler geri alınamaz, sadece değiştirilebilir

#### **3.5.2. Doğru Yaklaşım** {#3.5.2.-doğru-yaklaşım}

**Nasıl uygulanır:**

* Gizli bilgiler (secrets) çalışma zamanında (runtime) uygulamaya verilir  
* Ortam değişkeni (environment variable) veya merkezi gizli bilgi yöneticisi (secrets manager) kullanılır  
* Her ortamın (Dev/Test/Prod) gizli bilgileri (secrets) ayrıdır

**Pratik örnek:**

* Veritabanı parolası (database password) kodda değil, dağıtım (deploy) sırasında sisteme enjekte edilir (inject)

**Yaygın yanlışlar:**

* “.env dosyasını gitignore yaptık, sorun yok”  
* “Bu sadece test ortamı”

**Yeterli kabul edilen seviye:**

* Depo (repository) gizli bilgi taraması (secret scanning) temiz sonuç vermeli  
* Gizli bilgi döndürme (secret rotation) yapılabildiği gösterilmeli

---

## **4\. Uygulama Güvenliği (Application Security) Gereklilikleri** {#4.-uygulama-güvenliği-(application-security)-gereklilikleri}

### **4.1. Kimlik Doğrulama (Authentication)** {#4.1.-kimlik-doğrulama-(authentication)}

Kimlik doğrulama (authentication), bir kullanıcının **kim olduğunu doğrulama** sürecidir. Pratikte en sık yapılan hata, kimlik doğrulama (authentication) var diye sistemin güvenli olduğu varsayımıdır. Oysa zayıf kimlik doğrulama (authentication), tüm diğer güvenlik kontrollerini anlamsız hale getirir.

#### **4.1.1. Kimlik Doğrulama Neden Kritiktir?** {#4.1.1.-kimlik-doğrulama-neden-kritiktir?}

**Neden önemlidir:**

* Ele geçirilen bir hesap, içeriden yapılmış bir saldırı gibi davranır  
* Kimlik doğrulama (authentication) zayıfsa yetkilendirme (authorization), loglama (logging) ve izleme (monitoring) de etkisiz kalır

Geleneksel ekiplerde sık görülen yanlış düşünce:

“Kullanıcı adı ve şifre varsa yeterlidir”

#### **4.1.2. Standartlar (Zorunlu)** {#4.1.2.-standartlar-(zorunlu)}

**Nasıl uygulanmalı:**

* Tercihen merkezi tek oturum açma (SSO \- Single Sign-On) (OIDC / SAML) kullanılır  
* Bu mümkün değilse:  
  * Güçlü parola politikası (strong password policy) uygulanır  
  * Çok faktörlü kimlik doğrulama (MFA \- Multi‑Factor Authentication) zorunlu hale getirilir

**Güçlü parola politikası (strong password policy) minimumları:**

* Minimum uzunluk  
* Sözlük kelime engeli (dictionary word)  
* Tekrar kullanım engeli

**Yaygın yanlışlar:**

* Parola karmaşıklığını (password complexity) kullanıcıya bırakmak  
* MFA’yı sadece admin hesaplara vermek

**Yeterli kabul edilen seviye:**

* Kimlik doğrulama yöntemi dokümante edilmiş olmalı  
* MFA veya eşdeğeri ek koruma bulunmalı

#### **4.1.3. Oturum Yönetimi (Session Management)** {#4.1.3.-oturum-yönetimi-(session-management)}

Oturum yönetimi (session management), kimlik doğrulama (authentication) sonrası oluşan oturumun **nasıl korunduğunu** belirler.

**Nasıl uygulanır:**

* Oturum çerezi (session cookie) `Secure`, `HttpOnly` ve uygun `SameSite` bayrakları (flags) ile set edilir  
* Boşta zaman aşımı (idle timeout) ve mutlak zaman aşımı (absolute timeout) tanımlanır

**Yaygın yanlışlar:**

* Oturum süresini sınırsız bırakmak  
* Çıkış (logout) sonrası oturumu geçersiz kılmamak

**Yeterli kabul edilen seviye:**

* Oturum zaman aşımı değerleri yazılı olmalı  
* Çıkış (logout) sonrası oturum geçersizleştirme (session invalidation) doğrulanmalı

#### **4.1.4. Kaba Kuvvet (Brute Force) ve Kimlik Bilgisi İstismarı (Credential Abuse) Koruması** {#4.1.4.-kaba-kuvvet-(brute-force)-ve-kimlik-bilgisi-i̇stismarı-(credential-abuse)-koruması}

Kimlik doğrulama (authentication) uç noktaları (endpoints) en çok saldırıya uğrayan noktalardır.

**Nasıl korunur:**

* Hız sınırlama (rate limiting)  
* Başarısız giriş (failed login) sonrası gecikme veya hesap kilitleme (account lock)  
* Şüpheli davranışlar için alarm/uyarı (alert)

**Yaygın yanlışlar:**

* “CAPTCHA ekledik yeter”  
* Hız sınırlamayı (rate limit) sadece ön yüz (frontend)’de yapmak

**Yeterli kabul edilen seviye:**

* Kaba kuvvet (brute force) denemeleri loglanmalı  
* Anormal denemeler alarm üretmeli

---

### **4.2. Yetkilendirme (Authorization)** {#4.2.-yetkilendirme-(authorization)}

Yetkilendirme (authorization), bir kullanıcının **giriş yaptıktan sonra ne yapmaya yetkili olduğunu** belirler. Pratikte en çok güvenlik açığı bu alanda oluşur çünkü ekipler kimlik doğrulama (authentication) ile yetkilendirme (authorization)’ı karıştırır.

#### **4.2.1. En Az Ayrıcalık (Least Privilege) (Zorunlu)** {#4.2.1.-en-az-ayrıcalık-(least-privilege)-(zorunlu)}

En az ayrıcalık (least privilege) prensibi, bir kullanıcının veya sistem bileşeninin **işini yapmak için gereken minimum yetkiye sahip olması** gerektiğini söyler.

**Neden önemlidir:**

* Bir hesabın ele geçirilmesi durumunda hasar sınırlandırılır  
* Yanlışlıkla yapılan işlemler tüm sistemi etkilemez

**Nasıl uygulanır:**

* Roller açıkça tanımlanır (örnek: user, support, admin)  
* Her rolün hangi API’lere veya fonksiyonlara erişeceği yazılıdır  
* Varsayılan rol her zaman en kısıtlı roldür

**Yaygın yanlışlar:**

* “Şimdilik admin verelim sonra bakarız”  
* Ön yüz (frontend)’de buton gizleyip arka uç (backend)’de kontrol yapmamak

**Yeterli kabul edilen seviye:**

* Rol/yetki matrisi dokümante edilmiş olmalı  
* Arka uç (backend) tarafında her kritik işlemde yetki kontrolü yapılmalı

#### **4.2.2. Nesne Seviyesinde Yetkilendirme (Object‑Level Authorization) / IDOR** {#4.2.2.-nesne-seviyesinde-yetkilendirme-(object‑level-authorization)-/-idor}

IDOR (Insecure Direct Object Reference), bir kullanıcının **başkasına ait bir kaynağa erişebilmesi** anlamına gelir.

**Örnek senaryo:**

* Kullanıcı `/orders/123` çağırabiliyorsa  
* `/orders/124` çağırdığında başka birinin siparişini görebiliyorsa

**Nasıl önlenir:**

* Her kaynak (resource) erişiminde “bu kullanıcı bu objeye yetkili mi?” kontrolü yapılır  
* Sadece ID’ye güvenilmez

**Yeterli kabul edilen seviye:**

* Tüm okuma/güncelleme/silme (read/update/delete) işlemleri sunucu tarafı (server‑side) yetkilendirme kontrolü içerir  
* Bu kontroller test senaryolarında özellikle doğrulanır

---

### **4.3. Girdi Doğrulama (Input Validation) ve Çıktı Kodlama (Output Encoding)** {#4.3.-girdi-doğrulama-(input-validation)-ve-çıktı-kodlama-(output-encoding)}

Girdi doğrulama (input validation), kullanıcıdan veya dış sistemlerden gelen verinin **kontrollü şekilde kabul edilmesi** demektir. Bu kontrolü ön yüz (frontend)’e bırakmak güvenlik sağlamaz.

#### **4.3.1. Girdi Doğrulama Neden Arka Uçta (Backend) Olmalı?** {#4.3.1.-girdi-doğrulama-neden-arka-uçta-(backend)-olmalı?}

**Neden:**

* Ön yüz (frontend) tamamen atlanabilir  
* API’ler doğrudan çağrılabilir

Bu nedenle arka uç (backend), gelen her veriyi yeniden kontrol etmek zorundadır.

#### **4.3.2. Nasıl Uygulanır (Somut Anlatım)** {#4.3.2.-nasıl-uygulanır-(somut-anlatım)}

* İzinli liste (allow‑list) yaklaşımı kullanılır (beklenen formatlar tanımlanır)  
* Uzunluk, tip ve değer aralıkları kontrol edilir  
* Beklenmeyen veri reddedilir

**Yanlış örnekler:**

* Sadece regex ile kontrol etmek  
* Sadece ön yüz (frontend) doğrulaması

#### **4.3.3. Çıktı Kodlama (Output Encoding)** {#4.3.3.-çıktı-kodlama-(output-encoding)}

Çıktı kodlama (output encoding), sistemden çıkan verinin **başka bir bağlamda zararlı hale gelmesini** engeller.

**Neden gereklidir:**

* XSS saldırıları çoğunlukla buradan çıkar

**Nasıl uygulanır:**

* HTML, JSON, JavaScript bağlamına göre kodlama (encoding)  
* Framework varsayılanları (framework defaults)’nın bilinçli kullanımı

**Yeterli kabul edilen seviye:**

* Arka uç (backend) doğrulama (validation) mevcut olmalı  
* XSS testleri yapılmış olmalı

---

### **4.4. Loglama (Logging) ve Denetlenebilirlik (Auditability)** {#4.4.-loglama-(logging)-ve-denetlenebilirlik-(auditability)}

Loglama (logging), bir olay olduktan sonra **ne olduğunu anlayabilmenin tek yoludur**. Güvenlik ihlallerinin büyük kısmı ancak geriye dönük log analizi ile tespit edilir. Log yoksa olay yoktur; sadece fark edilmemiş bir ihlal vardır.

#### **4.4.1. Loglama Neden Güvenlik Konusudur?** {#4.4.1.-loglama-neden-güvenlik-konusudur?}

**Neden gereklidir:**

* Yetkisiz erişimler gerçek zamanlı fark edilemez  
* Olay sonrası kök neden analizi yapılamaz  
* Hukuki ve denetim süreçlerinde kanıt üretilemez

Geleneksel ekiplerde sık görülen yanlış düşünce:

“Log sadece hata ayıklamak içindir”

Bu yaklaşım yanlıştır. Loglama aynı zamanda **tespit edici kontrol (detective control)**’dür.

#### **4.4.2. Ne Loglanmalı? (Somut Liste)** {#4.4.2.-ne-loglanmalı?-(somut-liste)}

Minimum olarak aşağıdaki olaylar loglanmalıdır:

* Başarılı ve başarısız giriş (login) denemeleri  
* Çıkış (logout) işlemleri  
* Parola sıfırlama (password reset) ve MFA değişiklikleri  
* Rol ve yetki değişiklikleri  
* Admin veya ayrıcalıklı (privileged) işlemler  
* Kritik veri erişimleri (data access) ve dışa aktarma (export) işlemleri

**Yaygın yanlışlar:**

* Sadece hata (error) log tutmak  
* Hata ayıklama (debug) log’larını production’da açık bırakmak  
* Log’lara PII (Personally Identifiable Information) veya gizli bilgi (secret) yazmak

#### **4.4.3. Log Formatı ve İçeriği** {#4.4.3.-log-formatı-ve-i̇çeriği}

Her güvenlik ilgili (security‑relevant) log kaydı şu bilgileri içermelidir:

* Kim (user / service account)  
* Ne yaptı (action)  
* Ne zaman (timestamp)  
* Nereden (IP, source)  
* Sonuç (success / fail)

Bu bilgiler olmadan log, olay inceleme için yetersizdir.

#### **4.4.4. Denetim Kaydı (Audit Trail)** {#4.4.4.-denetim-kaydı-(audit-trail)}

Denetim kaydı (audit trail), belirli işlemler için **değiştirilemez ve silinemez** kayıt tutulması anlamına gelir.

**Nasıl uygulanır:**

* Kritik işlemler için özel denetim log’u (audit log) üretilir  
* Log’lar merkezi bir log platformuna gönderilir  
* Log silme yetkileri kısıtlıdır

**Yeterli kabul edilen seviye:**

* Log’lar merkezi sistemde toplanmalı  
* Saklama süresi (retention) yazılı olarak tanımlanmalı  
* Log erişimleri rol bazlı (role‑based) olmalı

---

### **4.5. Dosya Yükleme (File Upload) ve İçerik İşleme (Content Handling)** {#4.5.-dosya-yükleme-(file-upload)-ve-i̇çerik-i̇şleme-(content-handling)}

Dosya yükleme (file upload) fonksiyonları, basit gibi görünmesine rağmen en yüksek riskli alanlardan biridir. Kontrolsüz dosya yükleme; zararlı yazılım (malware) bulaşması, veri sızıntısı ve sistem ele geçirilmesine kadar giden sonuçlar doğurabilir.

#### **4.5.1. Dosya Yükleme Neden Risklidir?** {#4.5.1.-dosya-yükleme-neden-risklidir?}

**Neden:**

* Dosya içeriği ile dosya uzantısı aynı olmak zorunda değildir  
* Yüklenen dosyalar üzerinden zararlı kod çalıştırılabilir  
* Genel erişime açık depolama (public storage) ciddi sızıntı riskidir

Geleneksel ekiplerde sık görülen yanlış düşünce:

“Sadece PDF kabul ediyoruz, sorun olmaz”

#### **4.5.2. Nasıl Güvenli Yapılır?** {#4.5.2.-nasıl-güvenli-yapılır?}

**Minimum gereklilikler:**

* İzinli liste (allow‑list) yaklaşımı (izin verilen dosya tipleri net tanımlı)  
* Dosya boyutu limiti  
* Dosya adı temizlenir (sanitize) (path traversal engellenir)  
* Dosyalar doğrudan çalıştırılabilir dizinlerde tutulmaz

**Ek kontroller (gerektiğinde):**

* Zararlı yazılım taraması (malware scanning)  
* İçerik tipi doğrulaması (content‑type validation)

#### **4.5.3. Depolama (Storage) ve Erişim** {#4.5.3.-depolama-(storage)-ve-erişim}

**Nasıl uygulanır:**

* Yüklenen dosyalar özel depolama (private storage)’ta tutulur  
* Genel erişim gerekiyorsa süreli erişim (time‑limited access) kullanılır

**Yaygın yanlışlar:**

* Public bucket (public bucket) kullanmak  
* Yüklenen dosyayı doğrudan URL ile açmak

**Yeterli kabul edilen seviye:**

* Dosya yükleme kuralları dokümante edilmiş olmalı  
* Yetkisiz erişim testleri yapılmış olmalı

---

### **4.6. İş Mantığı İstismarı (Business Logic Abuse)** {#4.6.-i̇ş-mantığı-i̇stismarı-(business-logic-abuse)}

İş mantığı istismarı (business logic abuse), teknik olarak hatasız çalışan bir sistemin **yanlış veya kötü niyetli kullanımına** karşı savunmasız olmasıdır. Otomasyon ve scraping (scraping) saldırılarının büyük kısmı buradan gelir.

**İş Mantığı İstismarı Nedir:**

**Örnekler:**

* Sürekli deneme yaparak indirim veya kupon sömürülmesi  
* API’lerin otomatik script’lerle taranması  
* Sıralama, sayfalama (pagination) veya filtrelerin istismar edilmesi (abuse)

Bu tür saldırılar genellikle klasik zafiyet tarayıcıları (vulnerability scanners) tarafından yakalanmaz.

**Nasıl Önlenir:**

**Yaklaşım:**

* Normal kullanıcı davranışı tanımlanır  
* Anormal kullanım paterni (pattern)’leri belirlenir

**Somut önlemler:**

* Hız sınırlama (rate limiting)  
* Eşik (threshold) ve kota (quota) tanımları  
* Anomali bazlı alarm/uyarı (alert)

**Yaygın Yanlışlar:**

* “Bu özellik (feature) business konusu, güvenlik değil”  
* “Gerçek kullanıcı böyle yapmaz”

**Yeterli Kabul Edilen Seviye:**

* Kritik iş akışları (business flow)’lar için abuse senaryoları düşünülmüş olmalı  
* Hız sınırlama (rate limit) ve kontrol mekanizmaları uygulanmış olmalı

---

### **4.7 Zero Trust ve Mikro Segmentasyon Yaklaşımı** {#4.7-zero-trust-ve-mikro-segmentasyon-yaklaşımı}

Geleneksel güvenlik modeli, ağ içindeki bileşenlerin güvenilir olduğu varsayımına dayanır. Modern saldırılar ise çoğunlukla **ele geçirilmiş bir kimlik veya servis üzerinden yatay hareket (lateral movement)** ile ilerler. Zero Trust yaklaşımı, ihlalin kaçınılmaz olduğunu varsayarak **hasarı sınırlamayı** hedefler.

**Minimum gereklilikler:**

* Hiçbir kullanıcı, servis veya ağ bileşeni varsayılan olarak güvenilir kabul edilmez  
* Kimlik doğrulama ve yetkilendirme:  
  * Sadece kullanıcılar için değil  
  * Servisler arası iletişim için de uygulanır  
* Ağ erişimleri:  
  * En az ayrıcalık (least privilege) prensibi ile sınırlandırılır  
* Container ve bulut ortamlarında:  
  * Mikro segmentasyon uygulanır

**Nasıl uygulanır:**

* Kullanıcı erişimlerinde:  
  * Kimlik doğrulama sadece giriş anında değil, bağlam bazlı değerlendirilir. Bağlam bazlı değerlendirme; kimlik, cihaz, lokasyon veya davranışsal göstergelerden en az birini içermelidir.  
* Servisler arası iletişimde:  
  * Service account kullanımı  
  * Mutual TLS (mTLS) veya eşdeğeri güvenli kanal  
* Container/Kubernetes ortamlarında:  
  * Network policy’ler ile pod-to-pod iletişim sınırlandırılır  
* Bulut ortamlarında:  
  * Security group / firewall rule’lar sadece gerekli trafik için açılır

**Yeterli kabul edilen seviye:**

* Uygulamanın ağ ve servis erişim modeli dokümante edilmiş olmalı  
* Servisler arası iletişimde kimlik doğrulama bulunduğu gösterilebilmeli  
* Gereksiz ağ erişimleri teknik olarak engellenmiş olmalı

---

### **4.8 API Güvenliği (API Security)** {#4.8-api-güvenliği-(api-security)}

API’ler modern uygulamaların ana saldırı yüzeyidir. Klasik web güvenlik kontrolleri, API’lerde çoğu zaman **iş mantığı istismarı ve otomasyon saldırılarını** yakalayamaz.

**Minimum gereklilikler:**

Tüm dışa açık (internet-facing) API’ler:

* Merkezi bir gateway veya reverse proxy üzerinden yayınlanır

API’lerde:

* Kimlik doğrulama (authentication)  
* Yetkilendirme (authorization)  
* Hız sınırlama (rate limiting)  
   zorunludur

Nesne seviyesinde yetkilendirme (IDOR) kontrolleri uygulanır

**Nasıl uygulanır:**

* API Gateway veya eşdeğeri bir katman üzerinden OAuth2 / JWT / mTLS gibi standartlar uygulanır  
* Rate limiting için Kullanıcı, IP veya token bazlı tanımlanır  
* API response’ları gereğinden fazla veri döndürmez (data minimization)  
* API erişimleri detaylı şekilde loglanır

**Yeterli kabul edilen seviye:**

* API’lerin gateway üzerinden geçtiği mimari diyagram ile gösterilebilmeli  
* Rate limit ve abuse senaryoları threat modeling içinde ele alınmış olmalı  
* API log’ları olay analizi için yeterli detay içermeli

---

## **5\. DevOps ve CI/CD Gereklilikleri** {#5.-devops-ve-ci/cd-gereklilikleri}

DevOps ve CI/CD, güvenliğin otomatik ve sürdürülebilir hale getirildiği yerdir. Güvenlik kontrolleri boru hattı (pipeline) içine alınmadığında, kişiler değiştiğinde veya zaman baskısı oluştuğunda ilk vazgeçilen konu güvenlik (security) olur.

### **5.1. CI/CD Neden Güvenlik Konusudur?** {#5.1.-ci/cd-neden-güvenlik-konusudur?}

**Neden:**

* Manuel kontroller süreklilik göstermez  
* İnsan hatası kaçınılmazdır  
* “Sonra bakarız” denilen konular production’a taşınır

Bu nedenle güvenlik kontrolleri **kişilere değil, boru hattına (pipeline)** emanet edilmelidir.

---

### **5.2. CI Pipeline Minimum Güvenlik Kontrolleri (Zorunlu)** {#5.2.-ci-pipeline-minimum-güvenlik-kontrolleri-(zorunlu)}

Bu kontroller, her derleme (build)’de otomatik çalışmalıdır. Manuel çalıştırılan güvenlik aracı **yok hükmündedir**.

#### **5.2.1. SAST (Static Application Security Testing)** {#5.2.1.-sast-(static-application-security-testing)}

SAST, kod **çalıştırılmadan** önce yapılan statik analizdir (static analysis).

**Ne yakalar:**

* Koda gömülü gizli bilgi (hard‑coded secret)  
* Enjeksiyon (injection) riskleri  
* Güvensiz API kullanımları

**Ne yakalayamaz:**

* İş mantığı istismarı (business logic abuse)  
* Çalışma zamanı (runtime) konfigürasyon hataları

**Yaygın yanlışlar:**

* “SAST temiz geçti, sistem güvenli”  
* Hatalı pozitifleri (false positive) tamamen görmezden gelmek

**Yeterli kabul edilen seviye:**

* Kritik (Critical) bulgular düzeltme (fix) edilmeden birleştirme (merge) yapılmaz  
* Yüksek (High) bulgular için düzeltme (fix) veya risk kabulü (risk acceptance) vardır

---

#### **5.2.2. SCA (Software Composition Analysis)** {#5.2.2.-sca-(software-composition-analysis)}

SCA, kullanılan kütüphanelerin bilinen açıklar açısından taranmasıdır.

**Ne yakalar:**

* Bilinen CVE’ler  
* Lisans riskleri (gerektiğinde)

**Yaygın yanlışlar:**

* “Bu kütüphaneyi biz yazmadık”  
* “Güncelleme (upgrade) çok riskli”

**Yeterli kabul edilen seviye:**

* Kritik (Critical) CVE varken derleme (build) başarısız olmalı  
* Yüksek (High) CVE’ler için planlı aksiyon bulunmalı

---

#### **5.2.3. Gizli Bilgi Taraması (Secret Scanning)** {#5.2.3.-gizli-bilgi-taraması-(secret-scanning)}

Gizli bilgi taraması (secret scanning), depoya (repository) yanlışlıkla giren kimlik bilgileri (credential)’ları yakalamayı hedefler.

**Neden gereklidir:**

* İnsan hatası kaçınılmazdır  
* Git geçmişi (git history) geri alınamaz

**Yeterli kabul edilen seviye:**

* Boru hattı (pipeline) gizli bilgi bulunduğunda başarısız (fail) olmalı  
* Bulunan gizli bilgi derhal döndürülmeli (rotate)

---

### **5.3. DAST (Dynamic Application Security Testing)** {#5.3.-dast-(dynamic-application-security-testing)}

DAST, çalışan uygulamaya karşı yapılan dinamik güvenlik testidir.

**Ne yakalar:**

* Kimlik doğrulama (authentication) zayıflıkları  
* Enjeksiyon (injection) ve yanlış konfigürasyon (misconfiguration)

**Ne yakalayamaz:**

* Derin iş mantığı (business logic) hataları

**Yaygın yanlışlar:**

* Sadece bir kere çalıştırmak  
* Tüm bulguları hatalı pozitif (false positive) saymak

**Yeterli kabul edilen seviye:**

* İnternete açık (internet‑facing) uygulamalarda sürüm (release) öncesi çalıştırılmış olmalı  
* Bulgular değerlendirilmeli ve aksiyon alınmalı

---

### **5.4. Boru Hattı (Pipeline) Yeşilken Neden Risk Kalır?** {#5.4.-boru-hattı-(pipeline)-yeşilken-neden-risk-kalır?}

Boru hattı (pipeline) araçları **her şeyi yakalamaz**. Bu nedenle:

* Tehdit modellemesi (threat modeling)  
* Kod gözden geçirme (code review)  
* İş mantığı (business logic) analizi  
  boru hattı (pipeline) ile birlikte düşünülmelidir.

Boru hattı, güvenliğin tamamı değil; **temel otomatik katmanıdır**.

---

### **5.5. Sürekli Dağıtım (CD), Sürüm (Release) ve Geri Alma (Rollback)** {#5.5.-sürekli-dağıtım-(cd),-sürüm-(release)-ve-geri-alma-(rollback)}

#### **5.5.1. Sürüm (Release) Neden Güvenlik Konusudur?** {#5.5.1.-sürüm-(release)-neden-güvenlik-konusudur?}

Yanlış veya riskli bir sürüm (release), sistemin bilinçli olarak zayıflatılması anlamına gelir.

**Nasıl yönetilmeli:**

* Artifakt değişmezliği (artifact immutability) (build once, deploy many)  
* Onay mekanizması (approval gate)

#### **5.5.2. Geri Alma Stratejisi (Rollback Strategy) (Zorunlu \- Must)** {#5.5.2.-geri-alma-stratejisi-(rollback-strategy)-(zorunlu---must)}

Geri alma (rollback), sadece erişilebilirlik (availability) değil, **güvenlik olayı (security incident)** anında da kritik bir kontrol mekanizmasıdır.

**Neden:**

* Zafiyet içeren bir sürüm (release) hızla geri alınabilmelidir

**Yeterli kabul edilen seviye:**

* Geri alma yöntemi dokümante edilmiş olmalı  
* En az bir kez test edilmiş olmalı

---

### **5.6. Kod Olarak Altyapı (Infrastructure as Code) (IaC)** {#5.6.-kod-olarak-altyapı-(infrastructure-as-code)-(iac)}

IaC, altyapının manuel değil **kontrollü ve izlenebilir** şekilde yönetilmesini sağlar.

**Neden gereklidir:**

* Manuel değişiklikler iz bırakmaz  
* Yanlış konfigürasyon (misconfiguration) riski yüksektir

**Yeterli kabul edilen seviye:**

* Altyapı değişiklikleri IaC üzerinden yapılmalı  
* IaC için de güvenlik taraması (security scanning) uygulanmalı

---

### **5.7. Erişim Kontrolü (Access Control) ve Boru Hattı Güvenliği (Pipeline Security)** {#5.7.-erişim-kontrolü-(access-control)-ve-boru-hattı-güvenliği-(pipeline-security)}

CI/CD sistemleri yüksek yetkili sistemlerdir.

**Nasıl korunur:**

* En az ayrıcalıklı erişim (least privilege access)  
* MFA zorunluluğu  
* Denetim log’larının (audit logs) açık olması

**Yaygın yanlışlar:**

* Herkese admin boru hattı (pipeline) yetkisi vermek

---

### **5.8. Container ve Serverless Güvenliği** {#5.8.-container-ve-serverless-güvenliği}

Container ve serverless mimariler hızlıdır ancak yanlış yapılandırıldığında **yüksek etki alanına sahip güvenlik açıkları** oluşturur. Sadece kodun değil, **çalışma ortamının** da güvenliği sağlanmalıdır.

**Minimum gereklilikler:**

* Container imajları, CI pipeline’da zafiyet taramasından geçer  
* Base image’ler, güncel ve güvenilir kaynaklardan seçilir  
* Serverless fonksiyonlar, en az ayrıcalıklı IAM yetkileriyle çalışır

**Nasıl uygulanır:**

* CI pipeline içinde, container image vulnerability scan çalıştırılır  
* Runtime ortamında, anormal davranışlar (beklenmeyen network, dosya erişimi vb.) izlenir  
* Serverless için, event source validation ve input kontrolü uygulanır

**Yeterli kabul edilen seviye:**

* Container tarama raporları pipeline çıktısı olarak sunulabilmeli  
* Kritik zafiyet içeren imajlar canlıya alınmamalı  
* Runtime güvenliği için izleme ve ya alarm mekanizması tanımlı olmalı

---

## **6\. Test, Operasyon ve Canlıya Alma** {#6.-test,-operasyon-ve-canlıya-alma}

Bu bölüm, yazılımın sadece “çalışıyor” olmasının neden yeterli olmadığını açıklar. Amaç; ürünün canlıya alındıktan sonra **güvenli şekilde işletilebilir** olduğunu garanti altına almaktır.

### **6.1. Güvenlik Testi (Security Testing) Neden Ayrı Bir Konudur?** {#6.1.-güvenlik-testi-(security-testing)-neden-ayrı-bir-konudur?}

Güvenlik testleri (security tests), fonksiyonel testlerin doğal bir uzantısı değildir. Bir özellik (feature) doğru çalışabilir ama güvenli olmayabilir.

**Yanlış varsayım:**

“Test ekibi baktı, sorun yok”

Fonksiyonel testler, saldırgan gibi düşünmez.

---

### **6.2. Test Türleri ve Amaçları** {#6.2.-test-türleri-ve-amaçları}

#### **6.2.1. Birim (Unit) ve Entegrasyon (Integration) Testler** {#6.2.1.-birim-(unit)-ve-entegrasyon-(integration)-testler}

**Amaç:**

* Kodun beklenen şekilde çalıştığını doğrulamak  
* Regresyonları erken yakalamak

**Güvenlik açısından katkısı:**

* Yetkilendirme (authorization) ve doğrulama (validation) kontrollerinin kırılmadığını gösterir

**Yeterli kabul edilen seviye:**

* Kritik iş (business) ve güvenlik (security) fonksiyonları test kapsamındadır

---

#### **6.2.2. DAST (Sürüm Öncesi \- Release Öncesi)** {#6.2.2.-dast-(sürüm-öncesi---release-öncesi)}

DAST, çalışan uygulamaya dışarıdan bakar ve saldırgan perspektifi sunar.

**Neden gereklidir:**

* Çalışma zamanı (runtime) konfigürasyon hatalarını yakalar  
* Kimlik doğrulama (authentication) ve oturum (session) zayıflıklarını görür

**Yaygın yanlışlar:**

* Sadece bir kez çalıştırmak  
* Tüm bulguları hatalı pozitif (false positive) saymak

**Yeterli kabul edilen seviye:**

* İnternete açık (internet‑facing) uygulamalarda her sürüm (release) öncesi çalıştırılmış olmalı  
* Bulgular ön değerlendirme (triage) edilip aksiyon alınmış olmalı

---

#### **6.2.3. Sızma Testi (Penetration Test)** {#6.2.3.-sızma-testi-(penetration-test)}

Sızma testi (penetration test), otomatik taramaların yakalayamadığı **iş mantığı ve zincirleme riskleri** ortaya çıkarır.

**Ne zaman zorunlu:**

* Büyük sürüm (major release)  
* Yeni kimlik doğrulama (authentication) / ödeme (payment) / PII içeren özellik (feature)  
* İnternete açık (internet‑facing) kritik uygulamalar

**Yaygın yanlışlar:**

* Sızma testini uyum (compliance) kutusu (checkbox) olarak görmek  
* Raporu alıp aksiyon almamak

**Yeterli kabul edilen seviye:**

* Bulgular (findings) şiddet (severity) bazlı ele alınmalı  
* Düzeltme (fix) sonrası yeniden test (retest) yapılmış olmalı

---

### **6.3. Operasyonel Hazırlık** {#6.3.-operasyonel-hazırlık}

#### **6.3.1. İzleme (Monitoring) Neden Sadece Uptime Değildir?** {#6.3.1.-i̇zleme-(monitoring)-neden-sadece-uptime-değildir?}

Uygulama ayakta olabilir ama saldırı altında olabilir.

**İzleme (monitoring) şunları da kapsamalıdır:**

* Anormal giriş (login) denemeleri  
* Hata oranı (error rate) artışı  
* API istismarı (abuse) göstergeleri

**Yaygın yanlışlar:**

* Sadece CPU ve bellek (memory) izlemek

**Yeterli kabul edilen seviye:**

* Güvenlikle ilgili (security‑relevant) metrikler izleniyor olmalı  
* Alarm eşikleri tanımlı olmalı

---

#### **6.3.2. Olay Yönetimi (Incident Management)** {#6.3.2.-olay-yönetimi-(incident-management)}

Olay (incident), sadece sistemin çökmesi değildir; **güvenliği etkileyen her olaydır**. Tekrarlayan güvenlik olaylarında manuel müdahale:

* Geç tepkiye  
* Tutarsız aksiyonlara  
   neden olur.

**Nasıl hazırlanılır:**

Kritik olay türleri için:

* Olay müdahale senaryoları tanımlanır  
* Öğrenme ve iyileştirme hedeflenir

**Nasıl uygulanır:**

* Playbook’lar:

  * Credential compromise  
  * API abuse  
  * Data leakage senaryoları için hazırlanır  
* Düşük seviye olaylar:  
  * Otomatik veya yarı otomatik aksiyonlarla yönetilir

**Yeterli kabul edilen seviye:**

* Olay playbook’ları yazılı ve erişilebilir olmalı  
* Sev-1 / Sev-2 olaylar sonrası postmortem yapılmalı

---

#### **6.3.3. Olay Sonrası Değerlendirme (Postmortem) Kültürü** {#6.3.3.-olay-sonrası-değerlendirme-(postmortem)-kültürü}

Postmortem, suçlu bulma değil **öğrenme** sürecidir.

**Neden gereklidir:**

* Aynı hataların tekrarını önler  
* Süreç ve kontrol eksiklerini ortaya çıkarır

**Yeterli kabul edilen seviye:**

* Sev‑1 ve Sev‑2 olaylar için postmortem yapılmalı  
* Aksiyonlar takip edilmeli

---

#### **6.3.4. Threat Intelligence ve Proaktif İzleme (Operasyonel ve SOC Perspektifi)** {#6.3.4.-threat-intelligence-ve-proaktif-i̇zleme-(operasyonel-ve-soc-perspektifi)}

Güvenlik olayları çoğu zaman **bilinmeyen saldırılarla değil**, bilinen ancak fark edilmeyen tekniklerle gerçekleşir. Güncel tehdit bilgisiyle beslenmeyen izleme sistemleri, geç tepki verilmesine neden olur.

**Minimum gereklilikler:**

* Kurum, güncel zafiyet ve tehdit istihbarat kaynaklarını takip eder  
* Log ve izleme sistemleri, güvenlikle ilişkili göstergeleri analiz edebilir durumda olmalıdır

**Nasıl uygulanır:**

* CVE, vendor advisory ve sektörel uyarılar, güvenlik ekibi tarafından düzenli takip edilir  
* Log ve metrikler,IOC (Indicator of Compromise) bazlı incelenir  
* Şüpheli paternler, alarm veya manuel inceleme tetikler

**Yeterli kabul edilen seviye:**

* Threat intelligence takibinden sorumlu rol tanımlı olmalı  
* IOC bazlı analiz veya en azından manuel kontrol süreci bulunmalı  
* Kritik tehditler için aksiyon mekanizması tanımlı olmalı

---

### **6.4. Yedekleme (Backup) ve Geri Yükleme (Recovery)** {#6.4.-yedekleme-(backup)-ve-geri-yükleme-(recovery)}

Yedekleme (backup), hiç ihtiyaç duyulmaması umulan ama **test edilmeden güvenilemeyen** bir kontroldür.

**Nasıl ele alınmalı:**

* RPO ve RTO iş birimi (business) ile birlikte tanımlanır  
* Geri yükleme (restore) testleri düzenli yapılır

**Yaygın yanlışlar:**

* “Yedekleme alıyoruz” demek ama geri yükleme (restore) denememek

**Yeterli kabul edilen seviye:**

* Geri yükleme (restore) test sonuçları dokümante edilmiş olmalı

---

### **6.5. Canlıya Alma Kapısı (Go-Live Gate) (En Kritik Nokta)** {#6.5.-canlıya-alma-kapısı-(go-live-gate)-(en-kritik-nokta)}

Canlıya alma (go‑live), teknik bir karar değil; **bilinçli risk alma** kararıdır.

#### **6.5.1. Canlıya Alma Öncesi Zorunlu Kontroller (Zorunlu \- Must)** {#6.5.1.-canlıya-alma-öncesi-zorunlu-kontroller-(zorunlu---must)}

Aşağıdakiler tamamlanmadan canlıya çıkılamaz:

* Tehdit modellemesi (threat modeling) tamamlandı  
* Güvenlik gereksinimleri (security requirements) iş listesinde (backlog) tamamlandı  
* SAST / SCA / DAST sonuçları kabul edilebilir  
* Kritik (Critical) ve yüksek (High) riskler kapalı veya onaylı  
* Loglama (logging), izleme (monitoring) ve uyarı üretme (alerting) aktif  
* Geri alma (rollback) planı mevcut ve test edilmiş  
* Olay müdahalesi (incident response) ve çalıştırma kılavuzu (runbook) hazır

#### **6.5.2. Risk kabulü (Risk Acceptance)** {#6.5.2.-risk-kabulü-(risk-acceptance)}

Her risk kapatılamaz; ancak her risk **bilinçli şekilde kabul edilmelidir**.

**Nasıl yapılır:**

* Risk yazılı olarak tanımlanır  
* Etki ve olasılık belirtilir  
* Sorumlu (owner) ve bitiş tarihi (expiry date) atanır  
* ICT ve iş birimi (business) onayı alınır

---

### **6.6. Canlıya Alma Sonrası İlk 30 Gün** {#6.6.-canlıya-alma-sonrası-i̇lk-30-gün}

İlk 30 gün, en yüksek risk dönemidir.

**Beklentiler:**

* Artırılmış izleme (monitoring)  
* Hızlı geri bildirim (feedback) ve acil düzeltme (hotfix) yeteneği  
* Güvenlik log’larının aktif incelenmesi

**Yeterli kabul edilen seviye:**

* İlk 30 gün için sorumlular ve izleme planı tanımlı

---

## **7\. Canlıya Alma Güvenlik Kontrol Listesi (Go-Live Security Checklist) (Tek Sayfa – İmzalı)** {#7.-canlıya-alma-güvenlik-kontrol-listesi-(go-live-security-checklist)-(tek-sayfa-–-i̇mzalı)}

Bu bölüm, önceki tüm maddelerin **operasyonel karşılığıdır**. Amaç; canlıya alma (go‑live) kararını kişisel kanaatten çıkarıp **kanıta dayalı** hale getirmektir.

Bu kontrol listesi (checklist) tamamlanmadan canlıya çıkılamaz. İstisnalar yalnızca yazılı **risk kabulü (risk acceptance)** ile mümkündür.

---

### **7.1. Proje Bilgileri** {#7.1.-proje-bilgileri}

* Proje / Uygulama Adı:  
* Ortam: (Prod / External / Internal)  
* Sürüm (Release) Versiyonu:  
* Canlıya Alma Tarihi:  
* Geliştiren: (Kurum içi (in‑house) / Tedarikçi (vendor))

---

### **7.2. Secure SDLC Kontrolleri** {#7.2.-secure-sdlc-kontrolleri}

| Kontrol | Durum (Evet/Hayır \- Yes/No) | Kanıt (Evidence) | Not |
| ----- | ----- | ----- | ----- |
| Tehdit modellemesi (threat modeling) yapıldı |  |  |  |
| Güvenlik gereksinimleri (security requirements) iş listesinde (backlog) |  |  |  |
| Güvenli kodlama standardı (secure coding standard) uygulandı |  |  |  |
| Kod gözden geçirme politikası (code review policy) uygulandı |  |  |  |

---

### **7.3. Uygulama Güvenliği (Application Security) Kontrolleri** {#7.3.-uygulama-güvenliği-(application-security)-kontrolleri}

| Kontrol | Durum | Kanıt (Evidence) | Not |
| ----- | ----- | ----- | ----- |
| Kimlik doğrulama (authentication) güçlü (SSO/MFA) |  |  |  |
| Yetkilendirme (authorization) ve IDOR kontrolleri |  |  |  |
| Arka uçta (backend) girdi doğrulama (input validation) |  |  |  |
| Loglama (logging) ve denetim (audit) aktif |  |  |  |
| Dosya yükleme (file upload) kontrolleri |  |  |  |
| İş mantığı istismarı (business logic abuse) önlemleri |  |  |  |

---

### **7.4. DevOps ve CI/CD Kontrolleri** {#7.4.-devops-ve-ci/cd-kontrolleri}

| Kontrol | Durum | Kanıt (Evidence) | Not |
| ----- | ----- | ----- | ----- |
| SAST çalıştırıldı |  |  |  |
| SCA çalıştırıldı |  |  |  |
| Gizli bilgi taraması (secret scanning) temiz |  |  |  |
| DAST çalıştırıldı |  |  |  |
| Geri alma (rollback) planı test edildi |  |  |  |
| IaC kullanıldı |  |  |  |

---

### **7.5. Test ve Operasyonel Hazırlık** {#7.5.-test-ve-operasyonel-hazırlık}

| Kontrol | Durum | Kanıt (Evidence) | Not |
| ----- | ----- | ----- | ----- |
| Sızma testi (penetration test) (gerekiyorsa) |  |  |  |
| İzleme (monitoring) ve uyarı (alerting) aktif |  |  |  |
| Olay müdahalesi planı (incident response plan) hazır |  |  |  |
| Yedekleme (backup) ve geri yükleme (restore) test edildi |  |  |  |

---

### **7.6. Açık Riskler ve Risk kabulü (Risk Acceptance)** {#7.6.-açık-riskler-ve-risk-kabulü-(risk-acceptance)}

| Risk | Şiddet (Severity) | Önlem (Mitigation) | Sorumlu (Owner) | Bitiş Tarihi (Expiry) |
| :---: | :---: | :---: | :---: | :---: |
|  |  |  |  |  |

---

### **7.7. Canlıya Alma Onayı** {#7.7.-canlıya-alma-onayı}

Bu kontrol listesi (checklist)’te yer alan bilgilerin doğru olduğunu ve belirtilen risklerin bilinçli şekilde kabul edildiğini onaylarız.

| Rol | İsim | İmza | Tarih |
| ----- | ----- | ----- | ----- |
| ICT / Güvenlik (Security) |  |  |  |
| Operasyon (Operations) |  |  |  |
| İş Sahibi (Business Owner) |  |  |  |
| Tedarikçi (Vendor) (varsa) |  |  |  |

---

## **8\. Tedarikçi Güvenlik Eki (Vendor Security Annex) (Sözleşme Eki)** {#8.-tedarikçi-güvenlik-eki-(vendor-security-annex)-(sözleşme-eki)}

Bu bölüm, önceki tüm güvenlik gerekliliklerinin **hukuki ve bağlayıcı karşılığıdır**. Amaç; güvenli yazılım beklentisini iyi niyet veya sözlü mutabakat seviyesinden çıkarıp **sözleşmesel yükümlülük** haline getirmektir.

Bu ek, yazılım geliştirme, bakım veya destek hizmeti veren tüm tedarikçi (vendor)’lar için geçerlidir.

---

### **8.1. Genel Yükümlülükler** {#8.1.-genel-yükümlülükler}

#### **8.1.1. Kurumsal SDLC Zorunluluğu** {#8.1.1.-Kurumsal-sdlc-zorunluluğu}

Tedarikçi (vendor), geliştirdiği tüm yazılımlar için bu dokümanda tanımlanan **Kurumsal SDLC** gerekliliklerini uygulamakla yükümlüdür.

**Açıklama:**

* Tedarikçi kendi metodolojisini kullanabilir  
* Ancak çıktılar bu dokümanda belirtilen gerekliliklerle uyumlu olmak zorundadır

“Bizim standartlarımız farklı” gerekçesi kabul edilmez.

---

### **8.2. Güvenlik Gereklilikleri ve Teslimatlar (Deliverables)** {#8.2.-güvenlik-gereklilikleri-ve-teslimatlar-(deliverables)}

#### **8.2.1. Zorunlu Teslimat Listesi (Deliverable List)** {#8.2.1.-zorunlu-teslimat-listesi-(deliverable-list)}

Tedarikçi aşağıdaki çıktıları teslim etmekle yükümlüdür:

* Tehdit modellemesi (threat modeling) dokümanı  
* Güvenlik gereksinimleri (security requirements) listesi  
* SAST / SCA / DAST raporları  
* SBOM (gerekiyorsa)  
* Sızma testi (penetration test) raporu (gerekiyorsa)  
* Sürüm notları (release notes) ve güvenlik etkisi özeti (security impact summary)

Eksik teslimat (deliverable), teslimatın tamamlanmadığı anlamına gelir.

---

### **8.3. Zafiyet Yönetimi (Vulnerability Management) ve SLA** {#8.3.-zafiyet-yönetimi-(vulnerability-management)-ve-sla}

#### **8.3.1. Şiddet (Severity) Tanımları** {#8.3.1.-şiddet-(severity)-tanımları}

Şiddet (severity) seviyeleri ICT tarafından belirlenir:

* Kritik (Critical)  
* Yüksek (High)  
* Orta (Medium)  
* Düşük (Low)

Tedarikçi, şiddet (severity) sınıflandırmasını tek taraflı olarak değiştiremez.

#### **8.3.2. Düzeltme SLA (Fix SLA) (Örnek)** {#8.3.2.-düzeltme-sla-(fix-sla)-(örnek)}

| Şiddet (Severity) | Düzeltme Süresi (Fix Time) |
| ----- | ----- |
| Kritik (Critical) | X iş günü |
| Yüksek (High) | Y iş günü |
| Orta (Medium) | Z iş günü |
| Düşük (Low) | Planlı |

Bu süreler, ICT tarafından proje bazında güncellenebilir.

---

### **8.4. Canlıya Alma ve Ödeme Bağı** {#8.4.-canlıya-alma-ve-ödeme-bağı}

#### **8.4.1. Canlıya Alma Şartı** {#8.4.1.-canlıya-alma-şartı}

Bu dokümanda tanımlanan **Canlıya Alma Güvenlik Kontrol Listesi (Go‑Live Security Checklist)** tamamlanmadan canlıya çıkılamaz.

#### **8.4.2. Ödeme Koşulu** {#8.4.2.-ödeme-koşulu}

* Güvenlik teslimatları (security deliverables) teslim edilmeden kilometre taşı (milestone) tamamlanmış sayılmaz  
* Canlıya alma (go‑live) onayı olmadan final ödeme yapılamaz

---

### **8.5. Erişim ve Hesap Yönetimi** {#8.5.-erişim-ve-hesap-yönetimi}

Ayrıcalıklı hesaplar ele geçirildiğinde, tüm diğer güvenlik kontrolleri anlamsız hale gelir.  
 Bu nedenle admin erişimleri **istisna**, standart kullanıcı erişimleri **varsayım** olmalıdır.

**Minimum gereklilikler:**

* Ayrıcalıklı erişimler:  
  * Süreli (time-bound)  
  * İzlenebilir  
* Paylaşımlı admin hesapları yasaktır

### **Nasıl uygulanır:** {#nasıl-uygulanır:}

* Just-in-Time (JIT) erişim modeli  
* Ayrıcalıklı işlemler:  
  * Detaylı şekilde loglanır  
* Admin erişimleri:  
  * MFA ile korunur

### **Yeterli kabul edilen seviye:** {#yeterli-kabul-edilen-seviye:}

* Ayrıcalıklı erişim süreçleri yazılı olmalı  
* Admin işlemleri log’lar üzerinden izlenebilir olmalı

---

### **8.6. Gizlilik (Confidentiality) ve Veri Koruma (Data Protection)** {#8.6.-gizlilik-(confidentiality)-ve-veri-koruma-(data-protection)}

Tedarikçi:

* Kuruma ait verileri yalnızca sözleşme kapsamı içinde kullanabilir  
* Verileri üçüncü taraflarla paylaşamaz  
* Gerekli durumlarda veri maskeleme (data masking) ve şifreleme (encryption) uygular

---

### **8.7. Denetim ve Doğrulama Hakkı** {#8.7.-denetim-ve-doğrulama-hakkı}

Kurum:

* Tedarikçinin güvenlik uygulamalarını denetleme  
* Ek kanıt (evidence) talep etme  
* Gerekirse üçüncü taraf denetim yaptırma

haklarını saklı tutar.

Tedarikçi bu denetimleri makul süre içinde desteklemekle yükümlüdür.

---

### **8.8. İhlal Bildirimi (Breach Notification)** {#8.8.-i̇hlal-bildirimi-(breach-notification)}

Tedarikçi:

* Güvenlik ihlalini fark ettiği anda  
* En geç X saat içinde

kurumu bilgilendirmekle yükümlüdür.

Bildirim; olayın kapsamı, etkilenen sistemler ve ilk aksiyonları içermelidir.

---

### **8.9. Sözleşmesel Yaptırımlar** {#8.9.-sözleşmesel-yaptırımlar}

Bu ek kapsamında yer alan yükümlülüklerin ihlali durumunda:

* Canlıya alma (go‑live) ertelenebilir  
* Ödeme durdurulabilir  
* Sözleşme feshi gündeme gelebilir

---

## **9\. Gap Analysis Template (Mevcut Sistemler İçin)** {#9.-gap-analysis-template-(mevcut-sistemler-i̇çin)}

Bu bölüm, mevcut (legacy veya aktif) uygulamaların bu standartlara **ne kadar uyumlu olduğunu objektif şekilde ölçmek** için kullanılır. Amaç suçlu bulmak değil, **riskleri görünür kılmak ve önceliklendirmek**tir.

Gap analysis çıktıları; roadmap, bütçe ve kaynak planlaması için girdi olarak kullanılır.

---

### **9.3. Secure SDLC Gap Analizi** {#9.3.-secure-sdlc-gap-analizi}

| Kontrol | Durum | Açıklama | Risk Seviyesi | Aksiyon |
| ----- | ----- | ----- | ----- | ----- |
| Threat modeling mevcut |  |  |  |  |
| Security requirements backlog’da |  |  |  |  |
| Secure coding standard yazılı |  |  |  |  |
| Code review politika (policy) uygulanıyor |  |  |  |  |

---

### **9.4. Application Security Gap Analizi** {#9.4.-application-security-gap-analizi}

| Kontrol | Durum | Açıklama | Risk  Seviyesi | Aksiyon |
| ----- | ----- | ----- | ----- | ----- |
| Authentication güçlü (SSO/MFA) |  |  |  |  |
| Authorization & IDOR kontrolleri |  |  |  |  |
| Input validation backend’de |  |  |  |  |
| Loglama & audit yeterli |  |  |  |  |
| File upload kontrolleri |  |  |  |  |
| Business logic abuse önlemleri |  |  |  |  |

---

### **9.5. DevOps & CI/CD Gap Analizi** {#9.5.-devops-&-ci/cd-gap-analizi}

| Kontrol | Durum | Açıklama | Risk Seviyesi | Aksiyon |
| ----- | ----- | ----- | ----- | ----- |
| SAST pipeline’da |  |  |  |  |
| SCA pipeline’da |  |  |  |  |
| Secret scanning aktif |  |  |  |  |
| DAST düzenli çalışıyor |  |  |  |  |
| Rollback planı mevcut |  |  |  |  |
| IaC kullanılıyor |  |  |  |  |

---

### **9.6. Test ve Operasyon Gap Analizi** {#9.6.-test-ve-operasyon-gap-analizi}

| Kontrol | Durum | Açıklama | Risk Seviyesi | Aksiyon |
| ----- | ----- | ----- | ----- | ----- |
| Penetration test yapıldı |  |  |  |  |
| İzleme & alerting yeterli |  |  |  |  |
| Olay response planı |  |  |  |  |
| Yedekleme & restore testleri |  |  |  |  |

---

### **9.7. Risk Özeti ve Yol Haritası** {#9.7.-risk-özeti-ve-yol-haritası}

#### **9.7.1. Kritik Riskler (Öncelik 1\)** {#9.7.1.-kritik-riskler-(öncelik-1)}

| Risk | Etki | Önerilen Aksiyon | Hedef Tarih | Owner |
| :---: | :---: | :---: | :---: | :---: |
|  |  |  |  |  |

#### **9.7.2. Orta Vadeli İyileştirmeler** {#9.7.2.-orta-vadeli-i̇yileştirmeler}

| Alan | Mevcut Durum | Hedef | Planlanan Tarih |
| :---: | :---: | :---: | :---: |
|  |  |  |  |

#### **9.7.3. Kabul Edilen Riskler** {#9.7.3.-kabul-edilen-riskler}

| Risk | Gerekçe | Owner | Gözden Geçirme Tarihi |
| :---: | :---: | :---: | :---: |
|  |  |  |  |

---

## **10\. Gözden Geçirme, Versiyonlama ve Süreklilik** {#10.-gözden-geçirme,-versiyonlama-ve-süreklilik}

### **10.1. Gözden Geçirme Cycle** {#10.1.-gözden-geçirme-cycle}

* Doküman en az 6 ayda bir gözden geçirilir  
* Major incident sonrası güncellenir

### **10.2. Süreklilik** {#10.2.-süreklilik}

* Yeni projeler bu standarda göre başlar  
* Mevcut sistemler için gap analysis periyodik olarak güncellenir

---

## **11\. Canlıya Alma Öncesi Uyum (Compliance) Checklist** {#11.-canlıya-alma-öncesi-uyum-(compliance)-checklist}

Bu checklist, her canlıya alma öncesinde **zorunlu olarak doldurulmalı ve onaylanmalıdır**. Amaç; canlıya alma kararını kişisel kanaatten çıkarıp **standartlara uyuma dayalı, izlenebilir bir yönetişim (governance) kararı** haline getirmektir.

Bu tabloda yer alan tüm maddeler **Uyumlu** olarak işaretlenmeden canlıya alma onayı verilemez. İstisnalar yalnızca yazılı **risk kabulü (risk acceptance)** ile mümkündür.

---

### 

### **11.1. Uyum Checklist Tablosu** {#11.1.-uyum-checklist-tablosu}

| Standart Maddesi | Gereklilik | Mevcut Durum | Kanıt (Link / Doküman) | Uyumlu / Değil |
| ----- | ----- | ----- | ----- | ----- |
| 3.1.1 | Threat modeling tamamlanmış olmalı |  |  |  |
| 3.1.2 | Security requirements backlog’da yer almalı |  |  |  |
| 3.2 | Secure coding standard uygulanmalı |  |  |  |
| 3.3 | Code gözden geçirme politikası uygulanmalı |  |  |  |
| 3.4 | Dependency ve supply chain taramaları yapılmalı |  |  |  |
| 3.5 | Secrets management kurallarına uyulmalı |  |  |  |
| 4.1 | Authentication güçlü (SSO/MFA) olmalı |  |  |  |
| 4.2 | Authorization ve IDOR kontrolleri uygulanmalı |  |  |  |
| 4.3 | Backend input validation mevcut olmalı |  |  |  |
| 4.6 | Loglama ve audit mekanizmaları aktif olmalı |  |  |  |
| 4.7 | File upload kontrolleri uygulanmalı |  |  |  |
| 4.8 | Business logic abuse önlemleri alınmalı |  |  |  |
| 5.2 | CI/CD pipeline security kontrolleri çalışmalı |  |  |  |
| 5.3 | DAST çalıştırılmış ve değerlendirilmiş olmalı |  |  |  |
| 5.5 | Rollback planı mevcut ve test edilmiş olmalı |  |  |  |
| 6.2 | Gerekliyse penetration test yapılmış olmalı |  |  |  |
| 6.3 | İzleme ve olay yönetimi hazır olmalı |  |  |  |
| 6.4 | Yedekleme ve geri yükleme testleri yapılmış olmalı |  |  |  |

---

### 

### **11.2. Uyum Değerlendirme ve Onay** {#11.2.-uyum-değerlendirme-ve-onay}

Bu checklist’te yer alan maddelerin doğru şekilde değerlendirildiğini ve belirtilen durumun gerçeği yansıttığını beyan ederiz.

| Rol | İsim | İmza | Tarih |
| ----- | ----- | ----- | ----- |
| ICT / Security |  |  |  |
| Operations |  |  |  |
| Business Owner |  |  |  |
| Tedarikçi (varsa) |  |  |  |
