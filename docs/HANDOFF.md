# Estado

Rama: `feat/diagnostics` (sale de `feat/core-and-grant`, que sale de `master`).
Nada fusionado. 352 tests del núcleo en verde, build desplegado, API verificada
contra v1.4.8 con control negativo.

Dos sistemas terminados y verificados en campaña: **reparación de equipo** y
**desarrollo de skills**. El motor de compra (fase 2) no está empezado.

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

## Decisiones tomadas y NO implementadas

1. **La concesión debe dar equipo básico, no el mejor del techo.** Si la fase 1
   regala el tope, la fase 2 se queda sin nada que hacer. Sortear entre tier 2 y
   3. Tier 1 descartado: es literalmente la ropa de civil del bug.
2. **Separar maza de una y de dos manos** en el enum del núcleo.
3. **Battania deriva y se deja derivar** (decisión del usuario). Solo se corrige
   la montura, no el rol de arma.
4. **La variedad de objeto la da el mercado.** Condición: el motor de compra debe
   mirar el inventario de la ciudad concreta, no el catálogo global. Hoy
   `FindBest` escanea el catálogo entero y devuelve el primero del tier más alto,
   así que dos lores de la misma cultura y techo reciben la idéntica espada.
5. **El foco de un roto puede ser arbitrario.** `GetNextSkillToAddFocus` elige por
   límite de aprendizaje, y con las skills a cero eso es casi azar. Si aparecen
   reparados con especialidades absurdas, habría que sembrar también un reparto
   de foco coherente con su arquetipo.

## Hechos del juego verificados, con su prueba

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
11. El diagnóstico se separó de la producción **dos veces** (`CultureProfile`, y
    `GAPFOCUS` midiendo skills que gobierna el equipo).

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
