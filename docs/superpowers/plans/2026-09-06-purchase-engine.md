# Plan — Motor de compra (fase 2)

Diseño de referencia: `docs/superpowers/specs/2026-09-03-hero-loadout-fixer-design.md`,
secciones 7, 9 y 10. Ese diseño sigue siendo válido; este plan lo ejecuta y anota
lo que ha cambiado desde que se escribió.

## Lo que cambió desde la spec

**El techo de tier ya no es estático.** Cuando la spec se escribió, la skill de un
lord no crecía. Ahora sí, así que su techo sube a lo largo de la partida y el
motor de compra persigue una diana en movimiento. Eso es deseable y no requiere
nada especial, pero explica por qué un lord vuelve a comprar años después.

**El dinero no es la restricción.** El lord más pobre del mapa dispone de ~64.000
denares. El modelo de presupuesto de la sección 7 sigue siendo necesario para no
arruinar clanes, pero **casi nunca va a ser lo que limite una compra** — lo será
el techo de tier. No calibrar el presupuesto esperando que muerda.

**`ItemCatalog.FindBest` escanea el catálogo global.** Para comprar hay que leer
el `ItemRoster` de la ciudad concreta. Si se reutiliza el escaneo global, dos
lores de la misma cultura y techo compran la idéntica espada y se pierde la
variedad que justifica todo el motor.

## Prerrequisito bloqueante — HECHO

**T0. La concesión gratuita debe dar equipo básico, no el mejor del techo.**

`GrantService` concedía el mejor ítem dentro del techo. Para un lord de techo 6
eso es un equipo de ~476.000 denares regalado, y **el motor de compra se quedaba
sin nada que hacer**.

Implementado en `Core/GrantTier`: banda tier 2-3 acotada por el techo, y sorteo
estable derivado de `heroId + ranura` vía FNV-1a. Cubre armas, armadura,
**montura y guarnición** — la montura es la pieza más cara, y dejarla en
`FindBest` habría abierto por la puerta de al lado el agujero que T0 cierra.
Cuando la banda sale vacía cae a `FindBest`: una cultura sin nada en tier 2-3
debe seguir vistiendo a sus lores.

Efecto secundario buscado: dos lores de la misma cultura y techo ya no reciben la
idéntica espada.

## Tareas — todas implementadas, ninguna verificada en campana

**T1. `MarketScanner`** — HECHO. Lee el `ItemRoster` del asentamiento, una vez
por visita y no una vez por ranura. Las ofertas llevan el `EquipmentElement`
completo, no solo el `ItemObject`: una entrada del roster puede tener modificador
y hay que entregar exactamente lo que se ha cobrado. Precio via
`SettlementComponent.GetItemPrice`, con la party del heroe para que su Comercio
cuente igual que le cuenta al jugador.

**T2. `BudgetService`** — HECHO. El doble conteo esta resuelto: para un lider,
`OwnGold` devuelve cero porque `Clan.Gold` ya es su oro.

El ledger mide contra la bolsa **tal como estaba cuando la casa fue de compras
por primera vez ese dia**, no contra el saldo vivo. El oro se mueve en el
instante de la compra, asi que restar tambien el gasto del dia a un saldo vivo
cobraria dos veces cada denar. Efecto secundario deseable: un rescate que entra a
mediodia no reabre el presupuesto hasta manana.

**T3. `PurchaseService`** — HECHO. Comprueba stock y saldo, retira del roster,
transfiere oro con `GiveGoldAction`, equipa. El unico punto de fallo real esta
envuelto en try/catch que devuelve el objeto a la estanteria y deja al heroe como
estaba.

**T4. Venta de lo desplazado** — HECHO. Vuelve al roster de la ciudad y se paga
limitado por el oro del mercader. **Se reparte en la misma proporcion que la
compra**: si la casa puso el 60%, la casa recupera el 60%. Darselo todo al heroe
movia riqueza del lider a cada lord por debajo suyo, una vez por compra, durante
toda la campana.

**T5. Enganche** — HECHO. `AfterSettlementEntered`, solo ciudades, 25% por
visita, **una compra por visita**. Comprar un equipo entero en una tarde
deshace el sentido de que los lores se ganen su equipo.

**T6. Diagnostico** — HECHO. `HEADROOM` (cuantos tiers por debajo de su techo
esta cada lord, y con que bolsillo), `MARKET` (que hay en cada ciudad y cuanto
pasa el filtro de cultura) y `hlf.market <heroe>` para el detalle ranura a
ranura dentro de la ciudad donde este el jugador.

## Decisiones tomadas al implementar

- **El salto es de un tier entero.** Ordenar por valor del objeto habria puesto a
  quinientos lores cambiando de equipo en cada puerta por unos puntos.
- **Empate a tier, gana su propia clase de arma.** El catalogo empareja por
  familia, asi que una espada podia volverse maza; con el desempate solo pasa
  cuando la ciudad no tiene nada de su clase a ese tier.
- **Se arregla primero el hueco mayor**, la misma disciplina que las skills.
- **El clan del jugador queda fuera.** Para su casa `Clan.Gold` es su propio
  dinero, y unos companeros comprando armadura se lo gastarian sin preguntar. Es
  lo que dice la seccion 11 y es lo que evita repetir la queja que origino el mod.
- **Las facciones menores entran**, al reves que en la reparacion.

## Riesgos anotados

- **El ledger vive en memoria y se reinicia a diario.** Si un clan tiene cuatro
  lores comprando el mismo día, el cuarto debe ver el saldo ya comprometido por
  los tres anteriores. Es el caso que el ledger existe para cubrir y el que hay
  que probar explícitamente.
- **La reserva puede dejar a un clan sin comprar nunca.** Es intencionado —
  protege los salarios— pero conviene medir cuántos clanes quedan permanentemente
  bloqueados antes de dar el ritmo por bueno.
- **Revertir una transacción a medias.** El punto más delicado. Si el oro se
  transfiere y el equipar falla, el lord ha pagado por nada.

## Lo que falta

Todo esto esta escrito, compilado y desplegado, con 392 tests del nucleo en
verde y la API verificada. **Nada de ello se ha visto correr en una campana.**
Los once bugs de la fase 1 salieron todos de jugar, ninguno de leer codigo.

Orden de verificacion sugerido: censo primero (`HEADROOM` y `MARKET` dicen si hay
algo que comprar antes de mirar si se compra), luego `hlf.market` sobre un lord
concreto dentro de una ciudad, y solo despues dejar correr tiempo y mirar si el
hueco se cierra.

## Verificación

Núcleo puro con tests como hasta ahora. Y en campaña, la misma disciplina que ha
funcionado once veces: medir el hueco entre lo que un lord tiene y lo que podría
comprarse, y comprobar que ese hueco se cierra. Los percentiles agregados no
sirven para esto, como se aprendió con las skills.
