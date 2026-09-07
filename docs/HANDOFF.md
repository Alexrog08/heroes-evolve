# Estado

Rama: `feat/diagnostics` (sale de `feat/core-and-grant`, que sale de `master`).
Nada fusionado. 399 tests del núcleo en verde, build desplegado, API verificada
contra v1.4.8 con control negativo.

**Fases 1 y 2 cerradas y verificadas en campaña.** Tres sistemas: reparación de
equipo, desarrollo de skills y motor de compra. Este último con 460 compras
reales y cero transacciones fallidas, medido en dos campañas de edades opuestas.

Lo que queda sin verificar está en su propia sección al final; lo que se midió y
se decidió no hacer, también, para no volver a plantearlo sin datos nuevos.

## Lo que hace

### Reparar al noble mal generado

Un tick diario busca lores con el equipo degenerado y les rellena los huecos.
Detecta por **síntoma**, no por firma: menos de dos armas usables, o coraza o
casco de tier 1 (que es ropa de civil).

El arquetipo sale de tres cosas, en este orden:

1. **La skill que sobrevivió al fallo de generación.** Los rotos conservan una
   sola skill y un arma a juego —Nus solo Throwing, Zandina solo Polearm— y eso
   es lo único que el juego llegó a registrar sobre quién era ese héroe.
2. **Acotado por lo que su cultura fielda de verdad**, leído del árbol de tropas.
   Un vlandiano que dispara es ballestero: sus 18 tropas no tienen un solo arco.
3. **`DefaultFormationClass`** como respaldo cuando no hay skill que leer.

Nunca quita equipo puesto salvo la ropa de civil, que es el bug.

**Lo que concede es equipo basico, no el mejor de su techo.** Sortea entre tier
2 y 3 dentro de la cultura, con el sorteo derivado del id del heroe y de la
ranura: estable entre cargas, distinto entre lores. El techo es un limite de
*compra*; la reparacion solo tiene que vestir a un noble desnudo. Si concediera
el tope, un lord de techo 6 recibiria 476.000 denares de regalo y el motor de
compra se quedaria sin nada que hacer. Vale igual para la montura, que es la
pieza mas cara de todas.

### Desarrollar skills

**Combate**: crece la skill de cada arma equipada, por orden de ranura — el juego
mismo lee la ranura más baja para decidir qué entrena un héroe. Escudos y
munición no consumen rango.

**Todo lo demás**: crece según dónde la IA ya invirtió **foco**, que es su propia
declaración de para qué sirve ese lord. Sin foco no hay objetivo, y por eso el
72% que nunca invirtió en Herrería sigue sin saber forjar.

**Tres talentos independientes** (combate, civil, naval) derivados del id del
héroe. Uno de cada setenta destaca en los tres.

**Siembra al reparar**: un roto recibe de golpe las skills que su edad debería
haberle traído, porque a los 48 no puede pasarse otra década siendo inútil.

### Comprar equipo mejor

Un lord entra en una ciudad y, con un 25% de probabilidad, compra **una sola
cosa**: la mejor oferta que mejore una ranura que **ya lleva ocupada**.

Nunca vuelve a decidir el arquetipo. Lee lo que lleva puesto y busca algo mejor
del mismo tipo. No ejecuta el planificador, no mira skills para elegir rol y no
toca una ranura vacía. Un compañero equipado como arquero de infantería lo
seguirá siendo dentro de cuarenta años. **Nunca una lanza.**

- **El salto es de un tier entero**, no un statline mejor. Ordenar por valor
  tendría a quinientos lores cambiando de equipo en cada puerta.
- **La variedad la da el mercado**, no el catálogo: cada ciudad tiene su stock y
  comprar lo retira, así que el segundo lord que entra se lleva lo siguiente.
- **El dinero se mueve, no se crea.** Lo desplazado vuelve al roster de la ciudad
  y se reparte el ingreso en la misma proporción en que se pagó.
- **La línea del clan del jugador se traza en la party, no en el clan.** Quien
  va dentro de tu party no compra: lo equipas tú. Quien lidera una party propia
  —un lord con tropas, un compañero con una caravana— sí. Fuera de tu casa no
  hay condición de party.
- **Una salida de compras por lord y día**, marcada aunque no compre nada.

## El motor de compra, medido antes de verlo comprar

Censo sobre una campaña avanzada (525 lores elegibles, 57 ciudades).

**Dispara.** De 261 lores que estaban dentro de una ciudad en ese instante,
**119 comprarían algo ya mismo**. 65 están en o por encima de su techo en todas
las ranuras y el motor no tiene nada que hacer con ellos, que es correcto.

**El dinero no es la restricción y no lo será nunca.**

| | |
|---|---|
| Bolsillo disponible | p50 = **876.619**, mínimo 27.010 |
| Precio de lo que compraría | p50 = **2.924**, máximo 12.139 |

Trescientas compras de sobra para el lord mediano. La reserva y el ledger quedan
como seguridad, no como ritmo: el único freno real es el techo de tier.

**El hueco está en las botas; las compras serán armas.**

```
lores por detrás   Leg=337  Gloves=172  Cape=115  w0=106  Horse=88 ... Body=5 Head=2
comprarían         w3=30  Cape=22  w0=20  w2=16  Leg=11  w1=9  Gloves=7  Horse=4
```

No es contradicción, es el catálogo: hay **1125 cascos y 661 corazas, contra 108
grebas y 90 guantes**. El juego ya viste bien a los lores por arriba y los deja
descalzos, pero los mercados tampoco tienen grebas que venderles. Las armas
abundan (379 de una mano) y por eso son lo que acaba cambiando de manos.

**Body=5 y Head=2**: prácticamente nadie va mal de coraza ni de casco.

## La regla de cultura: medida y conservada

Cuesta, y bastante:

```
MARKET stockPerTown      p50=505
       forALocalLord     p50=454   (90%)
       forAForeignLord   p50=144   (28%)
```

Un lord fuera de casa ve el 28% del estante. **Aun así se queda**, porque el
reparto de bloqueos dice que no es lo que frena al motor:

```
SHOPPING blockedSlotsBy  wrongTier=103  culture=58
```

Dos de cada tres ranuras bloqueadas lo están por **escasez de tier**, que
relajar la cultura no arregla: en la ciudad de Amorcon había 23 grebas y ninguna
en la banda. `emptyShelf` y `skillOrUsage` salieron a **cero** las dos: el estante
nunca está vacío del todo y la dificultad nunca es el motivo.

Con el motor disparando ya para el 46% de los lores en ciudad, romper la
coherencia cultural —que es la espina dorsal de la fase 1— para desbloquear una
minoría de ranuras no sale a cuenta. Un lord bloqueado no lo está para siempre:
vuelve a casa, o pasa por otra ciudad.

**No volver a plantearlo sin datos nuevos.**

## Observación anotada, sin decidir

El techo va **sistemáticamente por debajo de lo que los lores ya visten**: techo
mediano 4, y Amorcon lleva tier 5 y 6 en seis ranuras con techo 4. El motor no
degrada a nadie, así que no hace daño, pero significa que **es un subidor de
suelo, no un perseguidor de techo**. Encaja con el motivo original —que un héroe
no sea más débil que las tropas que lidera— pero si algún día se quiere que los
lores lleguen a tier 5-6 comprando, lo que hay que tocar es `TierCeiling`, no el
motor.

## El motor de compra, verificado en campaña

**460 compras reales, 0 transacciones fallidas**, repartidas entre una campaña
madura y una joven.

**La economía no se resiente.** Dos ensayos independientes desde el mismo guardado
(se recargó entre medias, así que las lecturas de partida son idénticas al denar):

| | Inicio | Ensayo 1 | Ensayo 2 |
|---|---|---|---|
| `CLANGOLD p50` | 793.226 | **818.964** | **824.855** |

En los dos, los clanes acaban **más ricos que antes de comprar**. Los ingresos de
la IA superan al gasto en equipo con holgura, y el riesgo de drenaje que se temía
no existe. La reserva y el ledger nunca llegaron a morder.

**Lo que compran es sano.**

```
tier  1: 3    2: 6    3: 39    4: 133    5: 118    6: 17
ranura  w3=67  w1=64  w0=46  Cape=46  w2=44  Leg=20  Gloves=15  Horse=10  Body=3
```

El grueso en tier 4-5 y unas pocas piezas de tier 6 para las casas que pueden
pagarlas. Las armas dominan porque el catálogo las tiene: 379 de una mano contra
108 grebas en todo el juego. `sold=<nothing>` salió **0** — cada compra desplazó
una pieza real y la devolvió al estante.

**El dinero manda en el extremo alto, como se diseñó.** La compra más cara
registrada fue de 49.031; en la partida joven ningún precio pasó de 7.266 contra
un límite mediano de 9.138.

### La puerta del dinero, confirmada en una campaña pobre

La misma pregunta en una partida joven, donde ningún clan llega a los 433.000 que
hacen falta para una pieza de tier 6 al 10%:

| Compras por tier | Laboratorio (rica) | Partida joven |
|---|---|---|
| tier 6 | 17 | **0** |
| tier 5 | 118 | 7 |
| tier 4 | 133 | 40 |
| tier 3 | 39 | 49 |
| tier 2 | 6 | 41 |

**Cero piezas de tier 6 donde nadie puede pagarlas.** Es la progresión entera
funcionando: la campaña joven viste tier 2-4, la madura llega a tier 6, y la
diferencia la pone la cartera y no una regla escrita a mano.

El límite muerde de verdad y con suavidad: `boughtCheaperBecauseOfShare` 3,
`pricedOutByShare` 1-4, y un lord que renunció a una pieza 33.194 más cara. Casi
nadie se queda sin comprar; bajan a algo más barato, que es lo que se buscaba.

Tampoco drena aquí: **452.238 denares gastados en total** mientras la riqueza de
los clanes casi se dobla (`CLANGOLD p50` 88.776 a 161.716). El gasto en equipo es
ruido frente al crecimiento de la economía.

### Munición: el 36% de las compras de una campaña pobre

49 de 137 compras fueron flechas, y **todas** sustituían `default_arrows`. No es
un fallo: todo arquero empieza con flechas de tier 1, así que la munición es la
mejora más barata y más universalmente disponible que existe, y con la armadura
cara fuera de su alcance es lo que un lord pobre puede permitirse. Las
`bodkin_arrows_a` a tier 4 son además una mejora real y grande.

Anotado sin arreglar: entre ellas se cuelan `stealth_arrow` y `burning_arrows`,
munición de truco que el juego puntúa a tier 2 y que en combate probablemente sea
peor que la normal. Distinguirlas exigiría un modelo propio de calidad de arma
que contradijese el tier del propio juego, y esa es exactamente la clase de
criterio inventado que este proyecto evita.

## Los cuatro refinamientos del cierre

### El salto es media tier, medida sobre el tier fraccionario

El tier entero es un **redondeo**: desensamblado, `Tier = Clamp(Round(Tierf), 0, 6) − 1`.
Exigir un tier entero dejaba pasar una mejora de **0,02** que cruzaba la frontera
de redondeo (3,49 → 3,51) y rechazaba una de **0,98** que no la cruzaba
(3,51 → 4,49). El filtro dejaba pasar lo insignificante y bloqueaba lo grande.

Las compras comparan ahora `Tierf` en centésimas y exigen **50** de ganancia. Ese
número no es de gusto: es el punto medio entre lo que la regla vieja ya permitía
y lo que ya rechazaba. Más estricta en lo marginal, más laxa en lo grande.

El **orden** conserva el tier entero como primer criterio aunque el fraccionario
sea más preciso, y es deliberado: es lo que crea los empates que necesita la
regla de clase de arma. Ordenando por centésimas, una espada a 4,20 nunca empata
con un hacha a 4,35, el hacha gana siempre, y la espada del lord se vuelve maza.
**Grueso, carácter, precisión, precio.**

### Los perks de hacha y maza

De los **164 perks de arma** del juego, exactamente **dos** miran qué arma de una
categoría lleva el héroe en vez de la categoría: *Swift Strike* (una mano) y *On
The Edge* (dos manos), ambos «damage with axes and maces». Todos los demás dicen
«one handed weapons» o «polearms» y no distinguen una espada de un hacha.

No son una curiosidad: **202 y 184 lores de 403** los tienen. El perk **redefine
qué significa «su propia clase»** en el orden, en vez de añadir un quinto
criterio: sin perk la clase favorecida es la que lleva, con perk es el hacha o la
maza y la espada pasa a ser la deriva. Él eligió el perk; la espada se la dieron.

Se pregunta **por mano**, nunca en general: el enum del núcleo colapsa maza de
una y de dos manos, así que un lord con solo el perk de dos manos no debe ver
dirigida su ranura de una mano.

### El equipo que las tiendas no venden

`ItemObject.IsUniqueItem` **es falso para los 3.725 objetos del juego**. El primer
blindaje se construyó sobre ese flag y no protegía absolutamente nada;
instrumentarlo en vez de confiar en él es lo único que lo destapó.

La marca buena es `NotMerchandise`, y la pregunta no es «¿es especial?» sino
**«¿se lo podríamos haber vendido nosotros?»**. Son 283 objetos:

```
Banner=52  OneHandedWeapon=59  HeadArmor=46  Thrown=33  Horse=23
Polearm=13  TwoHandedWeapon=13  HorseHarness=8  ...  LegArmor=1
```

Las **52 banderas no cuentan**: el estandarte vive en `ExtraWeaponSlot` (índice
4) y el motor solo recorre `Weapon0..Weapon3`, así que esa ranura queda fuera de
su alcance por completo. La cifra que afecta al equipo son las ~231 restantes.

La reventa es **el único camino por el que este mod pone algo en un estante**. Que
un lord cambie su espada noble y esa espada esté esa tarde en el mercado de
Praven desmonta la exclusividad del equipo noble compra a compra, para toda la
campaña. Esa es la razón, más que la pérdida del héroe.

Medido: **366 lores, 622 piezas protegidas**, y un A/B sobre el mismo guardado
dice que el coste es **una compra de 48**. Esas piezas ya están cerca del techo y
el mercado no tenía nada mejor que ofrecer por ellas.

Y el modo de fallo del blindaje —dejar a alguien congelado en harapos— **no
ocurre**: `protectedPieceTier min=3 p50=6`. Ni una pieza protegida por debajo de
tier 3.

### El peso de la armadura: medido y descartado

Se planteó un tope de peso por arquetipo, tomando de referencia la tropa de élite
correspondiente, para que un arquero no acabara con la cota de un caballero.
**Medido y descartado**: la referencia dice lo contrario.

```
battania/elite  battanian_fian_champion   Ranged   37,00 kg   Body=23,00
empire/elite    imperial_elite_cataphract Cavalry  31,90 kg
vlandia/elite   vlandian_banner_knight    Cavalry  37,40 kg
```

El Fian batanio pesa **más** que el catafracto imperial. TaleWorlds quiso a su
arquero de élite blindado. «Los arqueros van ligeros» no es una regla que este
juego sostenga, y no había nada que arreglar.

### Estandartes: medido y cerrado, no se puede comprar

```
BANNER inCatalog=52  notMerchandise=52  buyable=0
BANNER townsStocking=0  bannersOnShelves=0
BANNER lordsCarrying=403  lordsWithout=0
BANNER wornTier  p50=2  p75=2  p90=4  max=6   (el catálogo llega a 6)
```

**Las 52 banderas del juego son `NotMerchandise` y ninguna ciudad tiene una en
stock.** El motor de compra no puede tocar esa ranura: no es cuestión de
implementarlo mejor, es que no hay mercancía. Y **los 403 lores llevan ya
estandarte**, así que tampoco falta ninguno.

Lo que sí hay es un hueco de calidad: casi todos llevan tier 2 (`phalanx_standard`,
`standard_of_duty`) mientras el catálogo llega a tier 6. Cerrarlo exigiría
**concederlos**, no venderlos — y eso es un buff de combate aplicado a todo el
mapa sobre un objeto que da bonus de formación, en una ranura que vanilla mantiene
deliberadamente fuera de la economía. **Decidido no hacerlo.**

La ranura del estandarte es `ExtraWeaponSlot` (índice 4) y el motor solo recorre
`Weapon0..Weapon3`, así que queda fuera de su alcance por construcción. Ver
`SlotMapping`.

## Lo que hay que seguir vigilando

**El hueco crece, no encoge.** `tiersBehind p50` sube de 6 a 7 dentro de un
ensayo. No es que no compren —`wouldBuyNow` cae de 143 a 118 en el mismo rato—
es que los techos suben detrás: un lord a 167 de skill cruza a 168 y gana un tier
entero de golpe en once ranuras. Con 28 puntos por tier y ~1 punto al año, a
largo plazo no debería desbocarse, pero no está medido en décadas.

**El coste de la regla de cultura sube en términos absolutos**, de 77 a 149
ranuras bloqueadas dentro de un ensayo. Pero `wrongTier` sube a la par (214 a
290) y **la proporción se mantiene en el 34-35%**. Mientras esa proporción no se
mueva, la decisión de conservarla sigue apoyada.

## Instrumentos

Para leer un motor callado, que es lo que más ha costado en este proyecto:

- `SHOPPING` — el que predice: qué lores comprarían ya, qué ranura, a qué precio
  y qué puerta bloquea a los demás. Es el único que pone un lord real en una
  ciudad real.
- `HEADROOM` — cuántos tiers por debajo de su techo está cada lord, y con qué
  bolsillo. **Sin hueco no hay nada que comprar, y el silencio es correcto.**
- `MARKET` — stock por ciudad, para un lord local y para uno de paso.
- `hlf.market <héroe>` — ranura a ranura dentro de la ciudad donde esté el
  jugador, con el motivo del bloqueo, sin comprar nada.

## Verificado en campaña

- 16 lores rotos reparados en la partida de laboratorio, sin errores.
- `Aran` y `Echa` cumplieron 18 durante la prueba y se repararon solos.
- La regla cultural desmonta 16 batanios y 10 nords, y **cero** del resto.
- **Combate cierra**: hueco mediano 33 → 24 en dos años, mientras los lores por
  detrás subían de 147 a 179.
- **No combate cierra** donde el efecto supera al ruido de entrada: Tactics
  28 → 15, Charm 27 → 18, Trade 29 → 22. Liderazgo, Scouting y Medicina salen
  planas, pero su recuento de rezagados subió un 30% en el mismo periodo, así que
  la medición no distingue "no cierra" de "cierra al ritmo que entra gente
  nueva". No demostrado; el motor es el mismo `Grant` ya probado en combate.
- **Nada se infla**: Herrería, Ingeniería y náuticas quietas en dos años.

## Calibración, y de dónde sale cada número

| Constante | Valor | Origen |
|---|---|---|
| `PeakNorm` | 150 | Los lores que TaleWorlds escribió están en 175-200; con 150 el lord medio termina en 196 y las generaciones se relevan sin empobrecerse |
| `Talent.Maximum` | 2.07 | El prodigio llega a 310, la marca del lord más fuerte que TaleWorlds escribió (309) |
| `Talent.Minimum` | 0.55 | Como el crecimiento nunca reduce, solo afecta a un roto sembrado desde cero |
| Madurez | 0.55 a los 18 → 1.0 a los 60 | El pico pertenece a los viejos |
| Forma por rango | 100/79/50/9% | Lo que lleva un lord sano: tres armas con valor real |
| `TypicalFocus` | 3 | La relación foco-valor es la misma dentro y fuera del combate: 3 de foco ≈ 108 |
| `CatchUpPerYear` | 0.12 | Con 0.33 alcanzaba el objetivo y se clavaba; con 0.12 se queda ~10 por detrás y sube siempre |

Trayectoria resultante con talento medio: 112 a los 20, 154 a los 40, 196 a los
60. Prodigio: 177, 243, 310. Entre 0,6 y 1,8 puntos ganados **cada año**, sin
tramos planos.

## Náutica: medido y decidido no cambiar

Las tres skills de War Sails crecen por foco, como las civiles. Se planteó
seguir en su lugar a los barcos que comanda el lord, por analogía con el arma
equipada. **Medido y descartado:**

```
con barcos   n=163  p50=20  p90=116
sin barcos   n=273  p50=19  p90=110
```

Mandar una flota no predice saber navegar. Y `CultureObject.NavalFactor` tampoco:
sturgia con 220% da mediana 19, igual que kuzait con 120%.

**La señal entera es ser nord**: mediana 134 y p90 228, contra 16-19 en las otras
seis culturas. NavalDLC siembra a sus lores como marinos y nadie más ha remado
nunca. El sistema de foco ya hace lo correcto — desarrolla a los nords que van
por detrás y deja al resto en 19, que es lo que el juego quiere.

No volver a plantearlo sin datos nuevos.

## Decisiones tomadas y NO implementadas

1. **Separar maza de una y de dos manos** en el enum del núcleo.
2. **Battania deriva y se deja derivar** (decisión del usuario). Solo se corrige
   la montura, no el rol de arma.
3. **La variedad de objeto la da el mercado.** Condición: el motor de compra debe
   mirar el inventario de la ciudad concreta, no el catálogo global. `FindBest`
   escanea el catálogo entero y devuelve el primero del tier más alto, así que
   dos lores de la misma cultura y techo comprarían la idéntica espada. La
   concesión ya no tiene ese problema —sortea dentro de la banda— pero la compra
   sí lo tendría si reutilizase el escaneo global.
4. **El foco de un roto puede ser arbitrario.** `GetNextSkillToAddFocus` elige por
   límite de aprendizaje, y con las skills a cero eso es casi azar. Si aparecen
   reparados con especialidades absurdas, habría que sembrar también un reparto
   de foco coherente con su arquetipo.

## Hechos del juego verificados, con su prueba

- **Lords Gear no sortea que el lord vaya a la ciudad.** Su
  `LordEquipmentBehavior::OnHourlyTick` recorre `Hero.AllAliveHeroes` **cada
  hora**, exige `hero.CurrentSettlement != null` e `IsTown`, y solo entonces tira
  `MBRandom.RandomInt(1, 101)` contra `AIShopVisitChance` o `ClanShopVisitChance`.
  El lord va donde lo lleva la IA del juego; el mod solo decide si compra estando
  ya allí. `OnDailyTick` limpia el conjunto de ids, así que hay **un tope de una
  compra por lord y día**. Nuestro enganche es por evento de entrada en vez de
  sondeo horario —más barato y una tirada por visita en vez de una por hora— con
  el mismo tope diario.

- `Clan.Gold` **es** `Leader.Gold`. Desensamblado: `get_Gold` → `get_Leader` →
  `Hero::get_Gold`. No hay bote de clan.
- El lord más pobre dispone de ~64.000 denares. **El dinero no será la
  restricción en la fase 2; lo será el techo de tier.**
- Los tiers de objeto no están en ningún XML: los calcula el juego al cargar.
- `ItemObject.PrimaryWeapon` es solo el uso cero. Toda bastarda crafteada se
  archivaba como de una mano: 8 espadas a dos manos de 3500 objetos.
- Los arcos largos no se usan montado, vía `item_usage="long_bow"`.
- `mule`, `sumpter_horse` y `pack_camel` son `Type="Horse"` y montables. Los
  distingue `is_pack_animal`.
- **Equitación 10** es el suelo de todo caballo de guerra.
- `EquipmentIndex.Head` comparte valor con `NumAllWeaponSlots`. Usar
  `SlotMapping.NameOf`.
- **`SandBoxCore/spcultures.xml` define `nord` con élite esturgia.** Es un
  marcador que NavalDLC sobrescribe en ejecución: el XML dice lo contrario que el
  juego.
- `CampaignTime` construye el calendario de `WeeksInSeason × DaysInWeek ×
  SeasonsInYear`, campos estáticos que un mod puede cambiar. **FastMode hace el
  año de 28 días.** Nunca cablear 12 semanas por año.
- `Hero.AddSkillXp` aplica el **factor de foco**; `ChangeSkillLevel` no. Por eso
  el crecimiento usa el primero —se compone con el juego, y el foco amplifica
  unas 3-4 veces— y la siembra el segundo, que es exacto.
- La IA reparte foco a **la skill que más sobrepasa su límite de aprendizaje**,
  así que empujar una skill hace que el juego persiga esa dirección solo.
- El tick diario de la IA (`DevelopCharacterStats`) solo **gasta** puntos y elige
  perks. No da XP.
- Los hijos del clan del jugador nacen con **25-27 puntos de atributo** contra
  18-21 del resto. La ventaja hereditaria existe.

## Bugs encontrados en campaña, no leyendo código

1. La espada dummy sobrevivía a la reparación.
2. Espada a dos manos y escudo a la vez — era una bastarda, y destapó que solo
   leíamos el uso primario.
3. Arco largo concedido a un héroe montado junto con el caballo.
4. `ReportGold` contaba dos veces el dinero de los líderes de clan.
5. El arquetipo por skills estaba **invertido**: lanza al arquero, arco al jinete.
6. `NeedsGrant` no detectaba ninguno de los 16 rotos de una partida real.
7. Lores montados en culturas sin caballería.
8. **`XpPerPoint = 12` inventado**: el crecimiento no movió nada en ocho pasadas.
9. **`CyclesPerYear = 12` cableado**: con FastMode corría a un tercio.
10. **Tope y suelo por ciclo en vez de por año**: el calendario se colaba por la
    puerta de atrás incluso tras leer `DaysInYear`.
11. El diagnóstico se separó de la producción **tres veces** (`CultureProfile`;
    `GAPFOCUS` midiendo skills que gobierna el equipo; y `MARKET` midiendo el
    stock contra la cultura de la ciudad en vez de la del lord, que devolvía un
    tranquilizador 90% cuando un lord de paso ve el 28%).
12. **`pricedOutByShare` contaba lo que no era**: lores que no podían comprar
    *nada*, que tras hacer la compra consciente del presupuesto casi no existen.
    Daba 2 y 0 mientras el límite gobernaba casi todas las compras.
13. **Los objetos sin tier leían como tier 0**, o sea peor que todo. Tres lores
    perdieron sus botes de nafta por una jabalina de 141 denares.

## Método

Instrumentar antes de decidir. Cuando la lectura del código y el comportamiento
observado se contradicen, la lectura está mal.

La lección más cara, que costó cuatro bugs: **un número que no sale de una
medición es un bug esperando.** `XpPerPoint`, `CyclesPerYear`, `PeakNorm` y los
topes por ciclo se eligieron los cuatro con buen criterio, y los cuatro estaban
mal.

Y cuando una métrica agregada no se mueve, medir **el hueco respecto al
objetivo** antes de concluir nada: eso distingue "no funciona" de "no hay nada
que hacer", y los percentiles de población no.
