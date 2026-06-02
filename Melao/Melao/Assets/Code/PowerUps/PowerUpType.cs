// Tipos de power-up de Melao. El orden NO importa para el inventario (se guarda
// por tipo), pero se usa como identificador estable en prefabs e iconos.
public enum PowerUpType
{
    Cuquis,   // dispara cuquis ilimitadas (controlado, no rafaga). Persistente.
    Heart,    // recupera una vida. Consumible.
    Chips,    // Pops se vuelve pequena (pasa por huecos). Consumible, temporal.
    Merengue  // Pops se vuelve grande (mas alcance/salto). Consumible, temporal.
}
