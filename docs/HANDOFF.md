# Estado al cerrar la sesión

Rama: `feat/diagnostics` (sale de `feat/core-and-grant`, que sale de `master`).
Nada fusionado. 263 tests del núcleo en verde, build desplegado, API verificada
contra v1.4.8 con control negativo.

## Lo siguiente que hay que hacer

Arrancar Bannerlord y ejecutar **`hlf.census`**. Requiere `cheat_mode = 1` en
`%USERPROFILE%\OneDrive\Documents\Mount and Blade II Bannerlord\Configs\engine_config.txt`.

El log sale en `%LocalAppData%\Mount and Blade II Bannerlord\logs\hlf.log`.

### Las tres líneas que deciden el diseño

```
FORMATION authored   ...
FORMATION generated  ...
FORMATION children   ...
```

`DefaultFormationClass` es la etiqueta de rol que TaleWorlds pone a mano a cada
lord. En `lords.xml`, 326 de 391 son `Cavalry`, Battania está invertida (37 de 40
`Ranged`), y no hay **ni un** lord vlandiano a distancia ni **ningún** ballestero.

- Si `children` sale con un reparto sensato, el arquetipo se resuelve leyendo esa
  etiqueta y las skills pasan a ser desempate.
- Si sale todo un valor por defecto, la etiqueta no sirve para los héroes que el
  mod repara —que son justo los nacidos en campaña— y hay que volver a las skills
  o al árbol de tropas.

### Lo demás que trae ese censo

- `CATALOG weapons` (identidad primaria) frente a `CATALOG accepted` (lo que el
  catálogo acepta de verdad). La diferencia entre ambos **es** el efecto del
  comodín de una-y-dos-manos. `TwoHandedSword` valía 8 sobre 3500 por uso
  primario; `accepted` debería ser mucho mayor.
- `TROOP` — firma de loadout de cada tropa tier 3+ por cultura. Sirve para saber
  qué armas usa cada rol en cada cultura, no para elegir el rol.
- `GOLD` — ya sin el doble conteo (ver abajo).
- `TIER` / `TIERSAMPLE` — ya volcados una vez; tier 1 es ropa de civil, tier 2 es
  militar ligero, tier 3 es armadura seria.

## Decisiones tomadas y no implementadas todavía

1. **La concesión gratuita debe dar equipo básico, no el mejor del techo.**
   Motivo: si la parte 1 regala el tope, la parte 2 (motor de compra) se queda
   sin nada que hacer. Sortear entre **tier 2 y 3**, aleatorio dentro de la
   cultura. Piezas disponibles sumando t2+t3: aserai 30, empire 29, vlandia 23,
   khuzait 21, battania 16, sturgia 14, **nord solo 5**.
   El techo de tier calculado pasa a ser exclusivamente el tope de compra.

2. **El arquetipo sale del rol del héroe, no de las tropas.** Pendiente de que
   `FORMATION children` lo confirme.

3. **Maza de una y de dos manos están colapsadas** en una sola categoría
   `Mace` del enum del núcleo. Por eso la regla de redundancia no se les puede
   aplicar. Habría que separarlas.

## Hechos del juego verificados esta sesión

- `Clan.Gold` **es** `Leader.Gold`. Desensamblado de v1.4.8:
  `get_Gold` → `get_Leader` → `Hero::get_Gold`, o 0 sin líder. No existe un bote
  de clan separado.
- El lord más pobre del mapa dispone de ~64.000 denares en partida nueva. Ninguno
  de los 462 baja de 10.000. **El dinero no va a ser la restricción**; lo será el
  techo de tier.
- Los tiers de objeto **no están en ningún XML**; los calcula el juego al cargar.
- `ItemObject.PrimaryWeapon` es solo el uso cero. La plantilla de forja
  `TwoHandedSword` ordena sus usos `OneHandedBastardSword, TwoHandedSword,
  OneHandedBastardSwordAlternative`, así que toda espada bastarda crafteada se
  archiva como de una mano.
- Los arcos largos no se pueden usar montado, y el juego lo dice vía
  `item_usage="long_bow"` (`ItemUsageSetFlags.RequiresNoMount`), no vía
  `WeaponFlags`.
- `EquipmentIndex.Head` comparte valor numérico con `NumAllWeaponSlots`;
  `ToString()` devuelve el alias. Usar `SlotMapping.NameOf`.
- Las culturas de mods (`nord`, de NavalDLC) atraviesan todo el sistema sin nada
  cableado.

## Bugs encontrados en campaña real, no leyendo código

1. La espada dummy de vanilla sobrevivía a la reparación: se clasificaba como
   espada legítima y el planificador planificaba a su alrededor.
2. Se concedía un arco largo a un héroe montado, junto con el caballo, en la
   misma pasada.
3. Etiquetas de ranura ilegibles en el log por el alias del enum.
4. `ReportGold` contaba dos veces el dinero de los líderes de clan.

Ninguno era visible leyendo el código.
