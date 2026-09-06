# Estado

Rama: `feat/diagnostics` (sale de `feat/core-and-grant`, que sale de `master`).
Nada fusionado. 291 tests del núcleo en verde, build desplegado, API verificada
contra v1.4.8 con control negativo.

La fase 1 —reparar al noble que el juego generó mal— **funciona y está
verificada en campaña real**. La fase 2, el motor de compra, no está empezada.

## Lo que hace hoy

Un tick diario por héroe busca lores con el equipo degenerado y les rellena los
huecos. Detecta por **síntoma**, no por firma:

- menos de dos armas usables, o
- coraza o casco de tier 1, que es ropa de civil

El arquetipo sale de `DefaultFormationClass`, la etiqueta que TaleWorlds pone a
mano a cada lord. El rol decide dos cosas: si va montado y si su arma principal
es a distancia. Las skills eligen todo lo demás.

Nunca quita equipo puesto, con **una excepción**: la ropa de civil, que es el
bug y no una decisión.

## Verificado en la partida de laboratorio (61 años de campaña, 600 lores)

- 16 lores rotos detectados y reparados por el tick diario, sin errores.
- `Aran` y `Echa` cumplieron 18 durante la prueba y se repararon solos. El bug
  ocurriendo en vivo.
- Los reparados reciben 1 o 2 piezas, no un equipo nuevo. Intervención mínima.
- La regla cultural desmonta 16 batanios y 10 nords, y **cero** de las otras
  cinco culturas.

## Decisiones tomadas y NO implementadas

1. **La concesión debe dar equipo básico, no el mejor del techo.** Si la fase 1
   regala el tope, la fase 2 se queda sin nada que hacer. Sortear entre **tier
   2 y 3**, aleatorio dentro de la cultura. Piezas t2+t3: aserai 30, empire 29,
   vlandia 23, khuzait 21, battania 16, sturgia 14, **nord solo 5**.
   Tier 1 está descartado: es literalmente la ropa de civil del bug.
2. **Separar maza de una y de dos manos** en el enum del núcleo. Hoy están
   colapsadas en `Mace`, y por eso la regla de redundancia de armas bastardas no
   se les puede aplicar.
3. **Battania deriva y se deja derivar.** Sus lores nacidos son 48% caballería
   contra 2,5% de los escritos a mano. Decisión del usuario: es evolución
   generacional y da variedad. Solo se corrige la montura, no el rol de arma.

4. **La variedad de objeto la da el mercado, no la concesion.** La fase 1 solo
   fija categorias; que dos lores acaben con espadas distintas depende de lo
   que hubiera en la ciudad donde compraron. **Condicion para que eso ocurra:**
   el motor de compra debe mirar el inventario de la ciudad concreta, no el
   catalogo global. Hoy `ItemCatalog.FindBest` escanea el catalogo entero y
   devuelve el primero del tier mas alto, asi que dos lores de la misma cultura
   y techo reciben la identica espada. Repetir ese patron en la fase 2
   reproduciria el determinismo cobrandolo.
   Lo que la fase 1 si fija para siempre es la *forma* del loadout, y eso sale
   de las skills, que ya varian solas.

## Hechos del juego verificados, con su prueba

- `Clan.Gold` **es** `Leader.Gold`. Desensamblado: `get_Gold` → `get_Leader` →
  `Hero::get_Gold`. No hay bote de clan; es el bolsillo del líder.
- El lord más pobre dispone de ~64.000 denares. **El dinero no será la
  restricción en la fase 2; lo será el techo de tier.** Las skills de los lores
  son bajas (80-150), así que los techos caen en tier 3-4.
- Los tiers de objeto no están en ningún XML: los calcula el juego al cargar.
- `ItemObject.PrimaryWeapon` es solo el uso cero. La plantilla de forja
  `TwoHandedSword` ordena sus usos `OneHandedBastardSword, TwoHandedSword, ...`,
  así que toda bastarda crafteada se archivaba como de una mano: 8 espadas a dos
  manos de 3500 objetos. Resuelto leyendo todos los usos.
- Los arcos largos no se usan montado, y el juego lo dice por
  `item_usage="long_bow"`, no por `WeaponFlags`.
- `mule`, `sumpter_horse` y `pack_camel` son `Type="Horse"` y montables. Los
  distingue `is_pack_animal`. Importa porque todo caballo de guerra exige
  Equitación 10 y ellos no exigen nada: un lord con 0 solo calificaba para mula.
- **Equitación 10** es el suelo de todo caballo de guerra, y la población tiene
  ahí su acantilado: ~18% en cero, nadie entre 5 y 15, el resto desde 20.
- `EquipmentIndex.Head` comparte valor con `NumAllWeaponSlots`. Usar
  `SlotMapping.NameOf`.
- **La élite de `nord` apunta a los druzhinniks esturgios** (NavalDLC), así que
  nord parece cultura de caballería desde ese ángulo. Por eso el árbol de tropas
  no sirve para decidir el perfil de los lores.
- `CultureFieldsMountedElites` recorre **una sola rama** del árbol. Sus
  respuestas son arbitrarias. No fiarse de él para nada nuevo.
- Perfil de montura por cultura, de los lores escritos a mano: khuzait 100%,
  empire 99%, vlandia 98%, aserai 94%, sturgia 94%, **battania 2%, nord 0%**.

## Bugs encontrados en campaña real, no leyendo código

1. La espada dummy sobrevivía a la reparación: se clasificaba como espada
   legítima y el planificador planificaba a su alrededor.
2. Espada a dos manos y escudo a la vez — resultó ser una bastarda, y destapó
   que solo leíamos el uso primario.
3. Arco largo concedido a un héroe montado, junto con el caballo, en la misma
   pasada.
4. `ReportGold` contaba dos veces el dinero de los líderes de clan.
5. El arquetipo por skills estaba **invertido**: lanza al arquero batanio y arco
   al jinete imperial.
6. `NeedsGrant` no detectaba ninguno de los 16 rotos de una partida real.
7. Lores montados en culturas sin caballería.

Ninguno era visible leyendo el código. Tres de ellos contradecían razonamientos
míos que parecían sólidos.

## Método que ha funcionado

Instrumentar antes de decidir. Cuando la lectura del código y el
comportamiento observado se contradicen, la lectura está mal: medir, no razonar.
Cada regla de este mod sale de un número del log, no de una intuición.
