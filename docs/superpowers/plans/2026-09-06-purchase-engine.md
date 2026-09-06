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

## Prerrequisito bloqueante

**T0. La concesión gratuita debe dar equipo básico, no el mejor del techo.**

Hoy `GrantService` concede el mejor ítem dentro del techo. Para un lord de techo 6
eso es un equipo de ~476.000 denares regalado, y **el motor de compra se queda sin
nada que hacer**. Sortear entre tier 2 y 3 dentro de la cultura, que el censo
mostró que es equipo militar de verdad y no ropa de civil.

Sin esto, la fase 2 no tiene efecto observable en los héroes reparados.

## Tareas

**T1. `MarketScanner`** — dado un asentamiento, los ítems de su `ItemRoster` que
pasan los filtros de categoría, cultura, techo y dificultad. Puro salvo la lectura
del roster. Devuelve candidatos con su precio local vía
`SettlementComponent.GetItemPrice`.

**T2. `BudgetService`** — envuelve `BudgetMath`, que ya está construido y probado.
Añade el ledger de gasto pendiente por clan, en memoria y reiniciado a diario, y
la reserva de seguridad. **Cuidado con el doble conteo**: `Clan.Gold` es
`Leader.Gold`, así que para un líder no se puede sumar su oro personal al del
clan. Ya mordió una vez en el diagnóstico.

**T3. `PurchaseService`** — la transacción. Verificar stock y saldo, retirar del
roster, transferir oro con `GiveGoldAction`, equipar. Cualquier fallo intermedio
revierte lo anterior. Nada de crear o destruir oro.

**T4. Venta de lo desplazado** — el ítem que sale vuelve al roster de la ciudad y
el oro va al héroe, limitado por lo que el mercader pueda pagar.

**T5. Enganche** — `CampaignEvents.AfterSettlementEntered`, con probabilidad por
visita (25% por defecto) en vez de cooldown, para escalonar el gasto.

**T6. Diagnóstico** — igual que en las fases anteriores, y por la misma razón: sin
instrumento no se puede distinguir "no compra porque no debe" de "no compra
porque está roto". Reportar por lord elegible: presupuesto disponible, techo,
candidatos encontrados en la ciudad, y por qué se descartó cada compra.

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

## Verificación

Núcleo puro con tests como hasta ahora. Y en campaña, la misma disciplina que ha
funcionado once veces: medir el hueco entre lo que un lord tiene y lo que podría
comprarse, y comprobar que ese hueco se cierra. Los percentiles agregados no
sirven para esto, como se aprendió con las skills.
