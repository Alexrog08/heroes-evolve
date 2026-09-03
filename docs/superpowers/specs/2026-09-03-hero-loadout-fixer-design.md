# HeroLoadoutFixer — Diseño

- **Fecha:** 2026-09-03
- **Estado:** aprobado, pendiente de plan de implementación
- **Juego objetivo:** Mount & Blade II: Bannerlord **v1.4.8** (build 119303)

## 1. Problema

Bannerlord tiene un fallo conocido y no resuelto: los héroes que llegan a la mayoría de edad durante la campaña a veces no reciben equipo y aparecen en batalla con ropa de civil y un arma sola.

Mecanismo verificado en el binario de v1.4.8:

1. `AgingCampaignBehavior::OnHeroComesOfAge` pide el equipo a `EquipmentSelectionModel::GetEquipmentForHeroComeOfAge`.
2. Ese modelo delega en `DefaultEquipmentSelectionModel::GetSuitableEquipmentSet`, que recorre `MBEquipmentRoster.All` filtrando por `IsRosterAppropriateForHeroAsTemplate` — cultura + `EquipmentCategories` + género.
3. Si nada coincide, devuelve null.
4. TaleWorlds conoce el caso: hay `Debug.FailedAssert("Battle equipment should not be empty", ..., "OnHeroComesOfAge", 304)` y un segundo assert en la línea 313.
5. El plan B es un roster hardcodeado por nombre: las lambdas del `Find` comparan `StringId` contra `generic_bat_dummy` y `generic_civ_dummy`.
6. `generic_civ_dummy` es, literalmente: `fine_town_tunic`, `iron_spatha_sword_t2` y `strapped_leather_boots`.

Ninguno de los 977 rosters que trae el juego declara el atributo `Flags` del que salen las categorías, así que no existe un arreglo por datos: no hay un ejemplo funcional que copiar.

Problema secundario: muchos héroes que ya existen en el mundo tienen ranuras de arma vacías que nunca se rellenan.

## 2. Objetivo

**Garantizar que todo héroe tenga un loadout de combate coherente con sus habilidades.**

### No-objetivos

- No compra ni vende nada. No hay economía.
- No mejora tiers de equipo ya presente. De eso se encarga NoblesBuyStuff con `Upgrade Equipped Items Only` activado.
- No tiene interfaz propia. El jugador edita el loadout de sus héroes con la pantalla de equipo normal del juego.

## 3. Decisiones de diseño

| Decisión | Elección | Razón |
|---|---|---|
| Modelo de preset | Categoría por ranura | Mapea 1:1 con cómo el juego guarda el equipo |
| Almacenamiento | **Ninguno** | El equipo puesto *es* el preset; la operación es idempotente |
| Origen del equipo | Se concede, no se compra | Funciona con lores arruinados, que son los peor equipados |
| Disparador | `DailyTickHeroEvent` | El juego ya lo escalona; sin barridos masivos |
| Ámbito por defecto | Solo lores de la IA | Tu clan lo vistes tú |

El preset no se almacena porque, mientras la regla sea "solo sustituir por la misma categoría", el equipamiento actual de un héroe reproduce siempre sus categorías originales. Derivarlo en cada uso da el mismo resultado que guardarlo, sin tocar el guardado ni arriesgar la compatibilidad de partidas.

## 4. Arquitectura

Seis componentes con fronteras explícitas:

| Componente | Responsabilidad | Dependencias |
|---|---|---|
| `SkillProfile` | Lee las 6 skills de combate + Riding, las devuelve ordenadas | `Hero.GetSkillValue` |
| `SlotInventory` | Clasifica `BattleEquipment`: categoría por ranura, vacías, montura, escudo, munición | `Equipment`, `WeaponClass` |
| `LoadoutPlanner` | **Función pura.** Perfil + inventario da un plan de pares (ranura, categoría) | ninguna |
| `ItemPicker` | Categoría + cultura + tier da un `ItemObject` concreto | `MBObjectManager` |
| `LoadoutApplier` | Aplica el plan. **Único punto que muta estado del juego** | `Equipment.set_Item` |
| `HeroLoadoutBehavior` | `CampaignBehaviorBase`, suscribe eventos, orquesta | `CampaignEvents` |

Que `LoadoutPlanner` sea puro es la decisión estructural más importante: toda la lógica no trivial queda testeable sin arrancar Bannerlord.

## 5. Reglas del planner

### 5.1 Algoritmo unificado

**No hay dos rutas.** El caso de las cuatro ranuras vacías es raro: un noble que cumple 18 y cae en el fallback de vanilla llega con el set `generic_civ_dummy`, es decir **con una espada de una mano ya equipada** y tres ranuras libres. Un algoritmo separado para «desnudo total» casi nunca se ejecutaría, y el que sí se ejecutaría no aplicaría la lógica de arquetipo. Por eso el mismo procedimiento cubre ambos casos, en tres pasos:

**Paso 1 — Planificar el objetivo.** Calcular el arquetipo ideal a partir de las skills, la montura y la viabilidad de proyectiles (5.2 y 5.3). Se calcula **siempre**, con independencia de lo que el héroe lleve puesto. Produce un conjunto objetivo de categorías, por ejemplo `{Arco, Munición, Munición, 1M}`.

**Paso 2 — Reconciliar con lo equipado.**

- Eliminar del objetivo las entradas que el equipo actual ya satisface.
- Descartar las entradas que el equipo actual contradice: si el objetivo pide escudo pero el héroe lleva equipada un arma de dos manos, el escudo se cae.
- **Nunca se retira nada de lo que ya lleva puesto.**

**Paso 3 — Rellenar.** Colocar las entradas restantes del objetivo en las ranuras vacías, por orden de prioridad del arquetipo. Coste en ranuras: melé 1, arrojadiza 1, arco 2, ballesta 2. Una entrada que no quepa se descarta y se pasa a la siguiente.

Si tras agotar el objetivo aún sobran ranuras, se sigue bajando por la lista de skills con el mismo criterio de coste y viabilidad, y después se aplica el **relleno de cortesía**: escudo (si no lleva y su arma principal no es de dos manos), luego munición extra (si porta proyectil y no lleva ya dos cargas), luego dejarla vacía. Una ranura vacía es un resultado válido: preferible a equipar algo incoherente.

La montura solo se evalúa en el paso 1, y solo cuando el héroe no tiene ninguna equipada.

Ejemplo del noble de 18 años con el set dummy y skills de arquero: el objetivo es `{Arco, Munición, Munición, 1M}`; la reconciliación tacha `1M` porque ya lleva la spatha; el relleno coloca arco y dos municiones en las tres ranuras libres. Resultado: arco + 2 municiones + spatha. La spatha sobrevive como arma secundaria y NoblesBuyStuff le subirá el tier más adelante.

### 5.2 Arquetipo objetivo

El sidearm de un arquero es el mayor entre OneHanded y TwoHanded. **Polearm queda excluido como acompañante de proyectil**: dos carcajes y una lanza no es un loadout real.

Con `dominancia = mejorSkillProyectil - mejorSkillMele` y margen configurable (por defecto 30):

| Caso | Loadout (4 ranuras) |
|---|---|
| Montado + proyectil dominante no utilizable a caballo (ver 5.3) | degradar a la siguiente categoría de proyectil viable; si ninguna lo es, tratar como melé dominante |
| Montado + proyectil viable | arma + 2 munición + sidearm |
| Sidearm de 2M | arma + 2 munición + 2M |
| Sidearm de 1M, dominancia mayor o igual que el margen | arma + 2 munición + 1M |
| Sidearm de 1M, dominancia menor que el margen | arma + 1 munición + escudo + 1M |

El escudo solo aparece acompañado de un arma de una mano.

Si la skill dominante es de melé, se siembran las primeras ranuras así:

- 2M dominante: 2M + 1M (sin escudo, no lo usaría con el arma principal)
- 1M o asta dominante: arma principal + escudo

Las tablas anteriores definen el **objetivo**, no el resultado final. Lo que de ese objetivo llega a equiparse lo deciden los pasos 2 y 3 de 5.1: se descarta lo ya satisfecho, se descarta lo contradicho por el equipo actual, y lo que queda se coloca en las ranuras libres. Un arquetipo de 2M dominante acabaría típicamente en 2M + 1M + arrojadiza + arrojadiza o lanza, según sus skills siguientes.

Armaduras: se rellenan las ranuras vacías de cabeza, cuerpo, piernas, manos y capa.

**Montura:** se concede si Riding está entre las dos mejores skills del héroe **o** si la línea de tropa élite de su cultura va montada. Lo segundo se determina recorriendo `CultureObject.EliteBasicTroop` y sus `UpgradeTargets`, comprobando si el tramo alto lleva un ítem con `HorseComponent.IsMount`. Así khuzaitas y vlandianos salen a caballo y battanios a pie sin cablear ninguna facción, y funciona con culturas moddeadas. Si se concede montura, se añade una barda compatible.

### 5.3 Viabilidad de proyectiles a caballo

**No se decide por clase de arma, sino por ítem.** Un arma de proyectil es utilizable a caballo si:

1. El héroe no tiene montura equipada — entonces cualquiera lo es; o
2. su `WeaponComponentData.WeaponFlags` **no** incluye `CantReloadOnHorseback`; o
3. es una **ballesta** y el héroe tiene `DefaultPerks.Crossbow.MountedCrossbowman`, consultado con `hero.GetPerkValue(...)`. La localización del juego describe ese perk literalmente como *"You can reload any crossbow on horseback."*, así que lo levanta para todas.

Para **arcos no se aplica ningún perk**. `DefaultPerks.Bow.MountedArchery` reduce la penalización de puntería a caballo; no consta que toque el flag de recarga, y ningún arco de vanilla lo lleva. Un arco moddeado que sí lo llevara se trata como no viable estando montado. El filtro solo puede hacernos descartar un arma, nunca equipar una inutilizable, y el descarte cae limpiamente a la siguiente skill.

Censo verificado sobre los ficheros de armas de un jugador de la v1.4.8:

| Tipo | `item_usage` | Ítems | Con `CantReloadOnHorseback` |
|---|---|---|---|
| Arco | `bow` | 11 | 0 |
| Arco | `long_bow` | 11 | 0 |
| Ballesta | `crossbow` | 5 | 5 |
| Ballesta | `crossbow_light` | 4 | 0 |
| Ballesta | `crossbow_fast` | 1 | 0 |

Las ballestas ligeras sí se recargan a caballo; solo las pesadas llevan el flag. Ningún arco de vanilla lo lleva. Y no es hipotético que los mods lo usen: con los módulos instalados en esta máquina, Open Source Weaponry aporta `AR_cheirosiphon_a`, de tipo `Crossbow` y **con** el flag. Por eso la regla lee el flag ítem a ítem y nunca asume por clase.

**Consecuencia arquitectónica.** `LoadoutPlanner` es puro y no conoce ítems, pero necesita saber si una categoría de proyectil es descartable para poder caer a la siguiente skill en lugar de dejar una ranura vacía. El llamante precalcula una estructura `MountedRangedAvailability { BowViable, CrossbowViable }` consultando el catálogo y los perks del héroe, y se la pasa al planner como entrada. El planner trata la categoría como no viable cuando el héroe está montado y esa bandera es falsa.

`ItemPicker` aplica el mismo filtro al elegir el ítem concreto: con héroe montado y sin perk, descarta candidatos que lleven `CantReloadOnHorseback`.

### 5.4 Tier objetivo

- Si el héroe lleva algo equipado: la mediana del tier de sus ítems actuales.
- Si va completamente desnudo: derivado de `Clan.Tier`.

Nunca se concede equipo por encima del tier que le correspondería.

## 6. Selección de ítem

`ItemPicker` recorre `MBObjectManager.Instance.GetObjectTypeList<ItemObject>()` y filtra por:

- categoría solicitada (clasificación por `WeaponClass`, con `ItemType` de respaldo)
- cultura del héroe, con respaldo a ítems sin cultura
- tier objetivo, aceptando un margen de mas/menos 1
- exclusión de `NotMerchandise`, ítems de misión y objetos crafteados por el jugador

Si no encuentra candidato, devuelve null: esa entrada del plan se descarta sin lanzar excepción.

## 7. Eventos

- `CampaignEvents.HeroComesOfAge`: ruta from-scratch si llega desnudo o con el set dummy.
- `CampaignEvents.DailyTickHeroEvent`: ruta gap-fill.

Ambos con `AddNonSerializedListener`. No hay evento de contratación de compañero: el jugador equipa a los suyos a mano, y el ámbito por defecto deja a su clan fuera.

## 8. Ajustes (MCM)

| Ajuste | Por defecto |
|---|---|
| Ámbito | Solo lores de la IA |
| Activar relleno de huecos | Sí |
| Activar construcción desde cero | Sí |
| Margen de dominancia de proyectil | 30 |
| Conceder monturas en construcción desde cero | Sí |
| Registro en fichero | No |

## 9. Persistencia

Ninguna. `SyncData` vacío, listeners no serializados, cero tipos serializables. Desinstalar el mod no rompe partidas.

La ausencia de almacenamiento es posible porque la operación es idempotente: "si hay hueco y hay skill viable, rellena". Ejecutarla repetidamente sobre el mismo héroe da el mismo resultado y sale de inmediato cuando no hay nada que hacer. El propio equipo del héroe hace de registro.

## 10. Convivencia con NoblesBuyStuff

No se pisan por construcción:

- HeroLoadoutFixer escribe **solo en ranuras vacías** (composición del loadout).
- NoblesBuyStuff con `Upgrade Equipped Items Only` sustituye **solo ranuras ocupadas** por la misma categoría (calidad del loadout).

Configuración recomendada de NBS para acompañarlo: `Upgrade Equipped Items Only` activado, y `Enable Horse Upgrades` desactivado si no se quiere que ningún héroe adquiera monturas por su cuenta.

## 11. Manejo de errores

- El trabajo de cada héroe va envuelto en try/catch. Un héroe que falle se registra y se salta; nunca tumba el tick ni la carga de partida.
- `ItemPicker` devuelve null en vez de lanzar.
- **Antes de compilar** se verifica que toda la API invocada existe en la v1.4.8 instalada, cruzando las referencias del ensamblado contra los ensamblados del juego. Este paso es obligatorio: DynamicLordGear v1.2.2 quedó inservible en 1.4.8 con `MissingMethodException` (HRESULT 0x80131513) porque `MBEquipmentRoster.HasEquipmentFlags` e `IsEquipmentTemplate` desaparecieron y `EquipmentCulture` pasó de campo a propiedad.

## 12. Testing

`LoadoutPlanner` es puro, así que se prueba con tests unitarios normales sin Bannerlord. Casos obligatorios:

1. Escudo + lanza + 1M con una ranura libre y Throwing como 3ª skill: jabalinas.
2. El mismo caso con Arco como 3ª skill: no cabe (coste 2, libre 1), salta a la 4ª.
3. Dos ranuras libres y Arco como 3ª skill: arco + munición.
4. Arquero dominante a pie con sidearm de 1M y dominancia alta: 2 munición, sin escudo.
5. Arquero dominante a pie con sidearm de 1M y dominancia baja: 1 munición + escudo.
6. Arquero dominante con sidearm de 2M: 2 munición, sin escudo.
7. Ballestero dominante con montura y solo ballestas pesadas disponibles: degrada. Con ballesta ligera disponible o con el perk MountedCrossbowman: mantiene la ballesta.
8. Héroe completamente desnudo: loadout de 4 ranuras coherente.
11. **Noble de 18 años con el set dummy** (solo una espada 1M equipada, 3 ranuras libres) y skills de arquero dominante: resultado arco + 2 municiones + la spatha conservada. Verifica que el arquetipo se aplica aunque no haya cuatro ranuras vacías.
12. Objetivo que pide escudo con un arma de dos manos ya equipada: el escudo se descarta en la reconciliación.
13. Objetivo cuya entrada ya está satisfecha por el equipo actual: no se duplica.
14. Arco moddeado con `CantReloadOnHorseback` y héroe montado: no viable, cae a la siguiente skill.
9. Héroe sin ranuras vacías: plan vacío, sin cambios.
10. Arquero: nunca se le asigna asta como acompañante.

El resto de componentes se verifica en juego mediante el registro opcional.

## 13. Fases de entrega

1. `SkillProfile`, `SlotInventory`, `LoadoutPlanner` y sus tests. Sin tocar el juego.
2. `ItemPicker`, `LoadoutApplier`, `HeroLoadoutBehavior` y ajustes MCM. Mod jugable.
3. Pulido y ampliación de casos.

## 14. Construcción

- Compilador: Roslyn 4.14 de las Build Tools de Visual Studio 2022, en `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe`.
- Destino: .NET Framework 4.x, DLL de biblioteca.
- Referencias: los ensamblados `TaleWorlds.*` del juego instalado, más `MCMv5` desde el módulo de Workshop.
- El script de build copia el resultado a `Modules\HeroLoadoutFixer\bin\Win64_Shipping_Client\` del juego.
