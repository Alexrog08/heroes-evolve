# Estado

Rama: `feat/diagnostics` (sale de `feat/core-and-grant`, que sale de `master`).
Nada fusionado. 392 tests del núcleo en verde, build desplegado, API verificada
contra v1.4.8 con control negativo.

Dos sistemas terminados y verificados en campaña: **reparación de equipo** y
**desarrollo de skills**. El **motor de compra** está escrito entero y no se ha
visto correr todavía.

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

**316 compras reales, 0 transacciones fallidas.**

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
