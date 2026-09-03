# HeroLoadoutFixer — Diseño

- **Fecha:** 2026-09-03
- **Estado:** aprobado, pendiente de plan de implementación
- **Juego objetivo:** Mount & Blade II: Bannerlord **v1.4.8** (build 119303)
- **Naturaleza:** mod **standalone**. No depende de NoblesBuyStuff ni de ningún otro mod.

## 1. Problema

### 1.1 El bug de la mayoría de edad

Los héroes que llegan a la mayoría de edad durante la campaña a veces no reciben equipo y aparecen en batalla con ropa de civil y un arma sola.

Mecanismo verificado desensamblando el binario de v1.4.8:

1. `AgingCampaignBehavior::OnHeroComesOfAge` pide el equipo a `EquipmentSelectionModel::GetEquipmentForHeroComeOfAge`.
2. Ese modelo delega en `DefaultEquipmentSelectionModel::GetSuitableEquipmentSet`, que recorre `MBEquipmentRoster.All` filtrando por `IsRosterAppropriateForHeroAsTemplate` — cultura + `EquipmentCategories` + género.
3. Si nada coincide, devuelve null.
4. TaleWorlds conoce el caso: hay `Debug.FailedAssert("Battle equipment should not be empty", ..., "OnHeroComesOfAge", 304)` y un segundo assert en la línea 313.
5. El plan B es un roster hardcodeado por nombre: las lambdas del `Find` comparan `StringId` contra `generic_bat_dummy` y `generic_civ_dummy`.
6. `generic_civ_dummy` es, literalmente: `fine_town_tunic`, `iron_spatha_sword_t2` y `strapped_leather_boots`.

Ninguno de los 977 rosters que trae el juego declara el atributo `Flags` del que salen las categorías, así que no existe arreglo por datos: no hay un ejemplo funcional que copiar.

### 1.2 Los lores no progresan

Vanilla no simula que los lores compren equipo. Un lord se queda con lo que le tocó al nacer o al ser generado, para siempre. No hay forma de distinguir de un vistazo a un comandante veterano de un recién llegado.

## 2. Objetivo

**Que el equipo de cada héroe cuente su historia: lo que sabe hacer, cuánto ha vivido y cuánto tiene su casa.**

Dos mitades:

- **Composición** — qué categorías lleva en cada ranura, derivado de sus habilidades.
- **Calidad** — el tier de cada pieza, comprado con dinero real y limitado por su mérito.

### No-objetivos

- No tiene interfaz propia. El jugador edita el loadout de sus héroes con la pantalla de equipo normal del juego.
- No toca al personaje del jugador. Nunca.
- No pretende crear personajes optimizados, sino legibles.

## 3. Decisiones de diseño

| Decisión | Elección | Razón |
|---|---|---|
| Modelo de preset | Categoría por ranura | Mapea 1:1 con cómo el juego guarda el equipo |
| Almacenamiento | **Ninguno** | El equipo puesto *es* el preset; la operación es idempotente |
| Equipo del bug de vanilla | Se concede gratis | Es reparar un fallo del juego, no economía |
| Todo lo demás | Se compra y se vende | Coherencia económica; el oro es la señal de riqueza |
| Techo de tier | Mezcla clan tier + skill | Adaptado de DynamicLordGear |
| Monedero | Héroe + clan, con reparto dinámico | Adaptado de Lords Gear |
| Ámbito por defecto | Solo lores de la IA | Tu clan lo vistes tú |

## 4. Arquitectura

| Componente | Responsabilidad | Dependencias |
|---|---|---|
| `SkillProfile` | Lee las 6 skills de arma + Riding, ordenadas | `Hero.GetSkillValue` |
| `SlotInventory` | Clasifica `BattleEquipment`: categoría por ranura, vacías, montura, escudo, munición | `Equipment`, `WeaponClass` |
| `LoadoutPlanner` | **Función pura.** Perfil + inventario da un objetivo de categorías | ninguna |
| `TierCeiling` | **Función pura.** Calcula el tier máximo que merece un héroe | ninguna |
| `BudgetService` | Presupuesto disponible, reparto del coste, ledger de gasto pendiente | `Hero.Gold`, `Clan.Gold` |
| `MarketScanner` | Inventario real de la ciudad visitada | `Settlement.ItemRoster` |
| `ItemPicker` | Categoría + cultura + tier + presupuesto da un `ItemObject` | `MBObjectManager` |
| `PurchaseService` | Compra, venta y transferencia de oro, con rollback | `GiveGoldAction`, `ItemRoster` |
| `GrantService` | Concesión gratuita para el caso del bug de vanilla | `Equipment.set_Item` |
| `HeroLoadoutBehavior` | `CampaignBehaviorBase`, suscribe eventos, orquesta | `CampaignEvents` |

`LoadoutPlanner` y `TierCeiling` son puros: toda la lógica de decisión queda testeable sin arrancar Bannerlord.

## 5. Composición del loadout

### 5.1 Algoritmo unificado

**No hay dos rutas.** El caso de las cuatro ranuras vacías es raro: un noble que cumple 18 y cae en el fallback de vanilla llega con el set `generic_civ_dummy`, es decir **con una espada de una mano ya equipada** y tres ranuras libres. Un algoritmo separado para «desnudo total» casi nunca se ejecutaría, y el que sí se ejecutaría no aplicaría la lógica de arquetipo. Por eso el mismo procedimiento cubre ambos casos, en tres pasos:

**Paso 1 — Planificar el objetivo.** Calcular el arquetipo ideal a partir de las skills, la montura y la viabilidad de proyectiles (5.2 y 5.3). Se calcula **siempre**, con independencia de lo que el héroe lleve puesto. Produce un conjunto objetivo de categorías, por ejemplo `{Arco, Munición, Munición, 1M}`.

**Paso 2 — Reconciliar con lo equipado.**

- Eliminar del objetivo las entradas que el equipo actual ya satisface.
- Descartar las entradas que el equipo actual contradice: si el objetivo pide escudo pero el héroe lleva equipada un arma de dos manos, el escudo se cae.
- **Nunca se retira nada de lo que ya lleva puesto**, salvo por sustitución directa en una mejora.

**Paso 3 — Rellenar.** Colocar las entradas restantes del objetivo en las ranuras vacías, por orden de prioridad del arquetipo. Coste en ranuras: melé 1, arrojadiza 1, arco 2, ballesta 2. Una entrada que no quepa se descarta y se pasa a la siguiente.

Si tras agotar el objetivo aún sobran ranuras, se sigue bajando por la lista de skills con el mismo criterio de coste y viabilidad, y después se aplica el **relleno de cortesía**: escudo (si no lleva y su arma principal no es de dos manos), luego munición extra (si porta proyectil y no lleva ya dos cargas), luego dejarla vacía. Una ranura vacía es un resultado válido: preferible a equipar algo incoherente.

La montura solo se evalúa en el paso 1, y solo cuando el héroe no tiene ninguna equipada.

Ejemplo del noble de 18 años con el set dummy y skills de arquero: el objetivo es `{Arco, Munición, Munición, 1M}`; la reconciliación tacha `1M` porque ya lleva la spatha; el relleno coloca arco y dos municiones en las tres ranuras libres. Resultado: arco + 2 municiones + spatha.

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

Las tablas anteriores definen el **objetivo**, no el resultado final. Lo que de ese objetivo llega a equiparse lo deciden los pasos 2 y 3 de 5.1.

Armaduras: se rellenan las ranuras vacías de cabeza, cuerpo, piernas, manos y capa.

**Montura:** se concede o compra si Riding está entre las dos mejores skills del héroe **o** si la línea de tropa élite de su cultura va montada, recorriendo `CultureObject.EliteBasicTroop` y sus `UpgradeTargets` hasta comprobar si el tramo alto lleva un ítem con `HorseComponent.IsMount`.

### 5.3 Viabilidad de proyectiles a caballo

**No se decide por clase de arma, sino por ítem.** Un arma de proyectil es utilizable a caballo si:

1. El héroe no tiene montura equipada — entonces cualquiera lo es; o
2. su `WeaponComponentData.WeaponFlags` **no** incluye `CantReloadOnHorseback`; o
3. es una **ballesta** y el héroe tiene `DefaultPerks.Crossbow.MountedCrossbowman`, consultado con `hero.GetPerkValue(...)`. La localización del juego describe ese perk literalmente como *"You can reload any crossbow on horseback."*

Para **arcos no se aplica ningún perk**. `DefaultPerks.Bow.MountedArchery` reduce la penalización de puntería a caballo; no consta que toque el flag de recarga. Un arco moddeado que sí lo llevara se trata como no viable estando montado. El filtro solo puede hacernos descartar un arma, nunca equipar una inutilizable.

Censo verificado sobre los ficheros de armas de un jugador de la v1.4.8:

| Tipo | `item_usage` | Ítems | Con `CantReloadOnHorseback` |
|---|---|---|---|
| Arco | `bow` | 11 | 0 |
| Arco | `long_bow` | 11 | 0 |
| Ballesta | `crossbow` | 5 | 5 |
| Ballesta | `crossbow_light` | 4 | 0 |
| Ballesta | `crossbow_fast` | 1 | 0 |

Las ballestas ligeras sí se recargan a caballo; solo las pesadas llevan el flag. Y no es hipotético que los mods lo usen: Open Source Weaponry aporta `AR_cheirosiphon_a`, de tipo `Crossbow` y con el flag.

**Consecuencia arquitectónica.** `LoadoutPlanner` es puro y no conoce ítems, pero necesita poder descartar una categoría para caer a la siguiente skill en lugar de dejar una ranura vacía. El llamante precalcula `MountedRangedAvailability { BowViable, CrossbowViable }` y se la pasa como entrada.

## 6. Techo de tier

Adaptado de `DynamicLordGear.GearSelector.GearSelectionParams.CalculateTargetGearTier`, cuya fórmula reconstruí del IL:

```
tierClan  = hero.Clan.Tier                          // 0–6
tierSkill = Math.Min(6, maxCombatSkill / 40)        // 40=T1, 80=T2 … 240=T6

techo = (tierClan·pesoClan + tierSkill·pesoSkill) / (pesoClan + pesoSkill)
techo = Math.Max(techo, TierMinimo)
```

`maxCombatSkill` es el máximo de OneHanded, TwoHanded, Polearm, Bow, Crossbow y Throwing. **Riding no entra**, igual que en DLG.

**Suelo duro adicional, que DLG no aplica:** nunca se compra ni concede un ítem cuyo `difficulty` supere la skill correspondiente del héroe. El juego solo gatea arcos (máx. 70), ballestas (máx. 70) y monturas (máx. 100); el resto tiene `difficulty` 0.

La diferencia con DLG es el uso: DLG usa este número para equipar mágicamente. Nosotros lo usamos como **techo de compra**. El lord converge hacia el equipo que merece, pero pagándolo.

Defaults propuestos: peso clan **0,5**, peso skill **1,0**, mínimo **1**.

## 7. Modelo económico

Adaptado de Lords Gear, con una salvaguarda que ese mod no tiene.

### 7.1 Monedero

```
disponible = hero.Gold + Math.Max(0, clan.Gold − gastoPendiente(clan) − reserva(clan))
```

`Clan.Gold` es, en el código del juego, literalmente `Clan.Leader.Gold`: no existe tesorería de clan separada. Y en `ClanVariablesCampaignBehavior.DailyTickClan` todo el ingreso del clan se aplica al **líder**, mientras que `DailyTickHero` solo paga a los **notables**. Por eso un lord no líder nunca acumula oro propio y el monedero **tiene que** incluir el del clan: usar solo `Hero.Gold`, como hace NoblesBuyStuff, deja a la mayoría de lores sin capacidad de compra.

### 7.2 Reparto del coste

Cada compra se reparte entre el clan y el héroe. Cuota del clan, por orden de aplicación:

| Condición | Cuota del clan |
|---|---|
| base | 60% |
| el héroe es el líder del clan | 70% |
| clan tier ≥ 1 | 40% |
| héroe con más de 2.000 de oro | 30% |
| clan con más de 40.000 de oro | 20% |
| | acotado entre **10% y 80%** |

Cuanto más rico es el héroe, más paga de su bolsillo; cuanto más rica la casa, más cubre ella.

### 7.3 Ledger de gasto pendiente

Un diccionario por `Clan.StringId` acumula lo comprometido en el ciclo actual y se resta del disponible. Evita que varios lores del mismo clan gasten cada uno sobre el saldo completo y lo drenen en cascada. **Se reinicia cada día y vive solo en memoria**, así que no añade datos a la partida.

### 7.4 Reserva de seguridad

```
reserva = DefaultClanFinanceModel.PartyGoldLowerThreshold × partidasDeGuerra(clan)
```

`PartyGoldLowerThreshold` vale **5.000** y es la constante del propio juego. Ni Lords Gear ni NoblesBuyStuff aplican nada parecido: verifiqué que la cadena no aparece en el binario de Lords Gear. Sin ella, un clan puede quedarse a cero y dejar de pagar salarios, con tropas desertando y lores en bancarrota.

Con reserva, el gasto se autolimita: cuando el saldo se acerca a la reserva el presupuesto cae a cero y los lores restantes esperan a que entren ingresos. Ese es el efecto de «normalización natural» deseado, garantizado por construcción.

### 7.5 Transacciones

- Precios locales vía `SettlementComponent.GetItemPrice`.
- El oro se **transfiere** con `GiveGoldAction.ApplyBetweenCharacters`, nunca se destruye ni se crea.
- Compra: verificar stock y saldo, retirar del `ItemRoster` del asentamiento, transferir oro, equipar.
- Venta de lo desplazado: devolver el ítem al `ItemRoster` del asentamiento y transferir oro al héroe, limitado por el oro del mercader.
- Cualquier fallo en un paso intermedio revierte los anteriores y deja equipo, stock y oro como estaban.

### 7.6 Ritmo

**Probabilidad por visita**, no cooldown. Al entrar en una ciudad, un héroe elegible tiene una probabilidad configurable de ir de compras. Escalona el gasto de forma más natural que un temporizador y evita que todos los lores de un clan compren el mismo día.

### 7.7 Contexto de magnitudes

El modelo de valor del juego es `valor ≈ 2.75^Tier × {100 ó 120} × factorApariencia`, decodificado de `DefaultItemValueModel`. De ahí:

| Tier | Precio por pieza | Kit completo (~11 piezas) |
|---|---|---|
| 3 | ~2.080 | ~23.000 |
| 4 | ~5.700 | ~63.000 |
| 5 | ~15.700 | ~173.000 |
| 6 | ~43.300 | ~476.000 |

El juego además grava con un 1% diario a los clanes con más de 100.000 de oro, transfiriéndolo al `KingdomBudgetWallet`. Es decir, espera que los clanes de la IA se muevan en decenas de miles. La curva exponencial hace que el tier 6 sea naturalmente raro sin necesidad de reglas extra.

## 8. Concesión gratuita

Único caso: el héroe tiene el equipo de batalla vacío o coincidente con la firma del set dummy de vanilla. Entonces se le concede gratis el loadout objetivo, al tier que le corresponda por el techo de la sección 6.

Es reparar un bug del juego, no economía. Y funciona con el lord arruinado que nunca pisa ciudad, que es el que peor está.

## 9. Selección de ítem

`ItemPicker` recorre el inventario de la ciudad (compra) o `MBObjectManager.Instance.GetObjectTypeList<ItemObject>()` (concesión) y filtra por:

- categoría solicitada (clasificación por `WeaponClass`, con `ItemType` de respaldo)
- cultura del héroe, con respaldo a ítems sin cultura
- tier menor o igual al techo
- `difficulty` menor o igual a la skill correspondiente del héroe
- viabilidad montada según 5.3
- precio dentro del presupuesto disponible
- exclusión de `NotMerchandise`, ítems de misión y objetos crafteados por el jugador

Si no encuentra candidato devuelve null: esa entrada del plan se descarta sin lanzar excepción.

## 10. Eventos

| Evento | Qué dispara |
|---|---|
| `CampaignEvents.HeroComesOfAgeEvent` | Concesión gratuita si llega desnudo o con el set dummy |
| `CampaignEvents.DailyTickHeroEvent` | Detección del caso dummy en héroes ya existentes; reinicio del ledger |
| `CampaignEvents.AfterSettlementEntered` | Compra y venta, sujeto a la probabilidad de visita |
| `CampaignEvents.HeroPrisonerTaken` | Pérdida de equipo al ser capturado (opcional, desactivado por defecto) |

Todos con `AddNonSerializedListener`.

## 11. Ajustes (MCM)

| Ajuste | Por defecto |
|---|---|
| Ámbito | Solo lores de la IA |
| Peso de clan tier en el techo | 0,5 |
| Peso de skill en el techo | 1,0 |
| Tier mínimo | 1 |
| Margen de dominancia de proyectil | 30 |
| Probabilidad de compra por visita | 25% |
| Multiplicador de la reserva de seguridad | 1,0 |
| Conceder monturas | Sí |
| Pérdida de equipo al ser capturado | No |
| Registro en fichero | No |

## 12. Persistencia

Ninguna. `SyncData` vacío, listeners no serializados, cero tipos serializables. Desinstalar el mod no rompe partidas.

Es posible porque la composición es idempotente —el propio equipo del héroe hace de registro— y porque el ledger de gasto pendiente es de sesión y se reinicia a diario. Lords Gear sí usa `SyncData`; nosotros lo evitamos deliberadamente.

## 13. Manejo de errores

- El trabajo de cada héroe va envuelto en try/catch. Un héroe que falle se registra y se salta; nunca tumba el tick ni la carga de partida.
- `ItemPicker` devuelve null en vez de lanzar.
- Toda transacción verifica precondiciones antes de mutar y revierte ante un fallo intermedio.
- **Antes de compilar** se verifica que toda la API invocada existe en la v1.4.8 instalada, cruzando las referencias del ensamblado contra los ensamblados del juego. DynamicLordGear v1.2.2 quedó inservible con `MissingMethodException` (HRESULT 0x80131513) porque `MBEquipmentRoster.HasEquipmentFlags` e `IsEquipmentTemplate` desaparecieron y `EquipmentCulture` pasó de campo a propiedad.

## 14. Testing

`LoadoutPlanner`, `TierCeiling` y `BudgetService` son puros o casi, y se prueban con tests unitarios sin Bannerlord.

Composición:

1. Escudo + lanza + 1M, una ranura libre, Throwing como 3ª skill: jabalinas.
2. El mismo caso con Arco como 3ª: no cabe (coste 2, libre 1), salta a la 4ª.
3. Dos ranuras libres y Arco como 3ª: arco + munición.
4. Arquero dominante a pie, sidearm de 1M, dominancia alta: 2 munición, sin escudo.
5. Arquero dominante a pie, sidearm de 1M, dominancia baja: 1 munición + escudo.
6. Arquero dominante con sidearm de 2M: 2 munición, sin escudo.
7. Ballestero dominante con montura y solo ballestas pesadas: degrada. Con ballesta ligera o con el perk: mantiene.
8. Héroe completamente desnudo: loadout de 4 ranuras coherente.
9. Héroe sin ranuras vacías: plan vacío.
10. Arquero: nunca se le asigna asta como acompañante.
11. Noble de 18 con el set dummy y skills de arquero: arco + 2 municiones + spatha conservada.
12. Objetivo que pide escudo con un arma de 2M equipada: el escudo se descarta.
13. Entrada del objetivo ya satisfecha: no se duplica.
14. Arco moddeado con `CantReloadOnHorseback` y héroe montado: no viable.

Economía:

15. Clan con 100.000 y 4 partidas: el tercer lord ve presupuesto reducido y el cuarto ve cero. El clan nunca baja de 20.000.
16. Ledger: dos lores del mismo clan en el mismo día no gastan cada uno sobre el saldo completo.
17. Reparto del coste: héroe con más de 2.000 de oro paga el 70%; líder de clan pobre paga el 30%.
18. Compra con stock insuficiente: no muta nada.
19. Venta con mercader sin oro: no muta nada.
20. Fallo intermedio en la transacción: equipo, stock y oro quedan como estaban.

Techo:

21. Clan tier 3 y skill 240 con pesos por defecto: techo 6.
22. Clan tier 6 y skill 80 con pesos por defecto: techo 4.
23. Ítem con `difficulty` superior a la skill: rechazado aunque el techo lo permita.

## 15. Fases de entrega

1. `SkillProfile`, `SlotInventory`, `LoadoutPlanner`, `TierCeiling` y sus tests. Sin tocar el juego.
2. `GrantService` y el arreglo del bug de vanilla. Mod ya útil.
3. `BudgetService`, `MarketScanner`, `ItemPicker`, `PurchaseService`. Motor de compra.
4. `HeroLoadoutBehavior`, ajustes MCM, pulido.

## 16. Construcción

- Compilador: Roslyn 4.14 de las Build Tools de Visual Studio 2022, en `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe`.
- Destino: .NET Framework 4.x, DLL de biblioteca.
- Referencias: ensamblados `TaleWorlds.*` del juego instalado, más `MCMv5` desde el módulo de Workshop.
- El script de build copia el resultado a `Modules\HeroLoadoutFixer\bin\Win64_Shipping_Client\`.

## Apéndice — Hallazgos verificados

Todo lo siguiente se obtuvo desensamblando los binarios instalados, no de documentación.

**Del juego (v1.4.8):**

- `Clan.Gold` es `Clan.Leader.Gold`. No hay tesorería de clan.
- `ClanVariablesCampaignBehavior.DailyTickClan` aplica el ingreso del clan al líder; `DailyTickHero` solo paga a notables. Los lores no líderes no tienen ingreso propio.
- `DefaultClanFinanceModel.PartyGoldLowerThreshold` = 5.000.
- Los clanes con más de 100.000 de oro pagan un 1% diario al `KingdomBudgetWallet`.
- `DefaultItemValueModel`: `valor ≈ 2.75^Tier × {100 ó 120} × apariencia`.
- `difficulty` máxima por tipo: arcos 70, ballestas 70, monturas 100, todo lo demás 0.
- Censo de los 24 lores con skills declaradas: mejor skill de combate mediana 210, máx 290; solo el 17% llega a 250.
- El nivel deriva de la suma de puntos de skill; la tabla se dimensiona para 1.000 niveles.

**De DynamicLordGear v1.2.2:**

- `CalculateTargetGearTier` mezcla clan tier y `Math.Min(6, maxCombatSkill / 40)` con pesos configurables y un suelo mínimo.
- Roto en v1.4.8: llama a `MBEquipmentRoster.HasEquipmentFlags` e `IsEquipmentTemplate`, eliminados, desde `GearCache.CacheLordLoadouts`, invocado en `OnSessionLaunch`.

**De Lords Gear v1.0.0:**

- `CalculateAvailableBudget` = `hero.Gold + Math.Max(0, clan.Gold − pendingClanSpend)`.
- `CalculateExpenseDistribution`: cuota del clan entre 10% y 80%, con umbrales en héroe > 2.000 y clan > 40.000.
- Ledger de gasto pendiente por clan, reiniciado a diario.
- Ritmo por probabilidad de visita, no por cooldown.
- Extiende `DefaultClanFinanceModel` para mostrar «Companion Gear (Clan Share)» en la pantalla de finanzas del jugador.
- **No aplica ninguna reserva de seguridad**: `PartyGoldLowerThreshold` no aparece en su binario.

**De NoblesBuyStuff v4.4.0:**

- `CalculateBudget` = `Hero.Gold × UpgradeBudgetPercent`. Solo el bolsillo del héroe, lo que lo hace poco efectivo para lores no líderes.
- Ritmo por cooldown en días. Tope de salto de tier por visita.
