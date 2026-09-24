# ITBees.Products - katalog produktów i magazyn urządzeń serializowanych

Biblioteka dostarcza gotowy moduł dla paneli administracyjnych ITBees (wzorzec jak w
ITBees.Inpost / ITBees.ServerStatus): encje, serwisy i kontrolery
(`[Authorize(Roles = "PlatformOperator")]`) dla:

- **katalogu** - producenci (`/Producer`, `/Producers`) i produkty (`/Product`, `/Products`),
- **magazynów** - `/Warehouse` (GET/POST/PUT/DELETE), `/Warehouses?isActive=`; magazyn, który był
  już używany, można tylko dezaktywować,
- **dostaw** - `/ProductDelivery` (GET/POST/PUT/DELETE), `/ProductDeliveries` (lista stronicowana):
  partia zakupowa = dane zakupu wspólne dla całej paczki (data zakupu, numer faktury, sprzedawca
  z NIP-em, okres gwarancji w miesiącach, magazyn) + zeskanowane numery seryjne w kolejności
  skanowania; zapis jest atomowy - jeden odrzucony numer odrzuca całą dostawę,
- **podpowiedzi sprzedawców** - `/ProductDeliverySellers?search=&limit=`: sprzedawcy zapamiętani
  z wcześniejszych dostaw (jeden wpis na parę nazwa + NIP, z danymi z ostatnio wprowadzonej
  dostawy; bez `search` - ostatnio używani). Sprzedawca nie jest osobną encją, więc nie ma tu
  tabeli ani migracji - a formularz zakupu może podpowiedzieć dostawcę bez NIP-u (np. z Chin),
  którego nie da się pobrać z GUS,
- **urządzeń na stanie** - `/SerializedProduct` (GET/PUT po guid), `/SerializedProducts` (lista):
  każde urządzenie oprócz numeru seryjnego producenta dostaje **nasz numer** (`Guid`),
- **etykiet magazynowych** - `/StockLabels?productDeliveryGuid=` albo `?guid=` - PDF, jedna
  strona = jedna etykieta **50 × 30 mm** (bez marginesów, w kolejności skanowania): kod QR oraz
  ostatnie 6 znaków naszego guida i ostatnie 4 znaki numeru seryjnego producenta; opcjonalnie
  (`PrintPurchaseDateOnLabels`, domyślnie wyłączone) także data zakupu.

Encje `SimCard` / `SimCardOperator` służą hostom, które wydają urządzenia z kartami SIM.

## Sprzedaż: ceny, widoczność, termin realizacji

Każdy produkt ma pola dla sklepu hosta (`ProductIm` / `ProductUm` / `ProductVm`):

| Pole | Znaczenie |
|---|---|
| `NetPriceSell` | cena netto sprzedaży jednej sztuki |
| `VatPercentageSell` | stawka VAT w procentach (0–100) |
| `GrossPriceSell` | cena brutto – to, co widzi i płaci klient |
| `IsPubliclyAvailable` | „publicznie dostępny” – host może pokazać produkt klientom i przyjąć zamówienie |
| `OrderFulfillmentDays` | „termin realizacji zamówienia” w dniach roboczych, gdy produktu nie ma na stanie; `null` = brak |

Cena brutto może różnić się od „netto + VAT” najwyżej o grosz (`ProductPrices.GrossTolerance`), żeby dało
się ustawić „ładną” cenę (1999,00 zł przy 23% = 1625,20 netto); większa różnica = 400. Pominięta
(`null`) wylicza się z netto i VAT – dlatego klient, który nie zna pola, przy każdej edycji ceny netto
utrzymuje brutto w zgodzie. W `PUT /Product` brak `IsPubliclyAvailable` / `OrderFulfillmentDays`
oznacza „zostaw bez zmian”, a `OrderFulfillmentDays = 0` czyści termin. Zaokrąglenia jak na fakturze:
do grosza, połówki od zera (`ProductPrices.GrossFromNet` / `NetFromGross`). Biblioteka nie wystawia
publicznej listy produktów – co i jak pokazać anonimowo, decyduje host (np. własny kontroler sklepu).

## Podłączenie w aplikacji hosta

```csharp
// DependencyRegistration:
new ITBees.Products.Setup.ProductsSetup().Register(builder.Services, new ProductsSettings
{
    // Strona panelu ze szczegółami urządzenia - do adresu doklejany jest guid urządzenia
    // (albo podstawiany w miejsce "{guid}", np. "https://host/device?id={guid}").
    // To właśnie ten link trafia do kodu QR etykiety. Bez adresu QR zawiera sam guid.
    DeviceWarehouseUrl = configuration["DeviceWarehouseUrl"],

    // Trzeci wiersz etykiety z datą zakupu. Domyślnie false - etykieta pokazuje tylko
    // końcówkę naszego guida i końcówkę numeru seryjnego.
    PrintPurchaseDateOnLabels = false
});

// DbContext.OnModelCreating:
ITBees.Products.Setup.DbModelBuilder.Register(modelBuilder);
// + wygeneruj migrację EF
```

Wymagania: generyczne repozytoria ITBees (`IReadOnlyRepository<>` / `IWriteOnlyRepository<>`),
`IAspCurrentUserService` (ITBees.UserManager) i rola `PlatformOperator`. Host z jawną listą
kontrolerów musi dopisać kontrolery z `ITBees.Products.Controllers` do swojej rejestracji.

## Aktualizacja z 8.0.8

Zmiany są addytywne; po podbiciu pakietu wygeneruj migrację – dojdą kolumny `Product.GrossPriceSell`,
`Product.IsPubliclyAvailable` (domyślnie `false`) i `Product.OrderFulfillmentDays` (nullable).
Istniejące wiersze mają `GrossPriceSell = 0`; `ProductVm` pokazuje dla nich netto + VAT, a w migracji
można je od razu uzupełnić:

```sql
UPDATE Product SET GrossPriceSell = ROUND(NetPriceSell * (100 + VatPercentageSell) / 100, 2)
WHERE GrossPriceSell = 0;
```

## Aktualizacja z 8.0.2

Zmiany są addytywne; po podbiciu pakietu wygeneruj migrację - pojawią się w niej:

- tabela `ProductDelivery`,
- kolumny `SerializedProductOnStock.Guid` (nullable + unikalny indeks - starsze wiersze nie mają
  jeszcze naszego numeru), `ProductDeliveryGuid`, `PositionInDelivery`,
- klucze obce `Product → UserAccount (AddedBy)` i `SerializedProductOnStock → Product` zmieniają
  się z `CASCADE` na `RESTRICT`: fizyczne usunięcie konta, które kiedyś dodało produkt, nie może
  po cichu skasować produktu i całego jego stanu magazynowego.

`ProductIm` / `ProductUm`: pola opcjonalne (`Ean`, `Model`, `DescriptionUrl`, `LongDescription`,
`Thumbnail`, `ProductImages`) są teraz nullable - przy włączonym `<Nullable>` MVC traktowało je
jako `[Required]` i odpowiadało 400, gdy klient je pominął. W `PUT /Product` brak miniatury lub
listy zdjęć oznacza „zostaw bez zmian".

## Etykiety - szczegóły techniczne

PDF powstaje bez zewnętrznych bibliotek i bez fontów na serwerze (standardowa Helvetica,
działa w gołym kontenerze Linux); kod QR jest wektorowy (ZXing.Net, korekcja M), a moduł kodu
to całkowita liczba punktów drukarki 203 dpi. Dokument ma ustawione `PrintScaling: None` -
drukuj w skali 100% na papierze 50 × 30 mm. Teksty na etykiecie są ograniczone do ASCII.
