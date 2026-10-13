using UnityEngine;

namespace Ratones.Basic
{
    // Cuadro rosado (solo en la vista de escena) que marca la parte de la cocina donde se juega.
    // Se mueve o escala en el editor y luego "Ratones/Ajustar cocina a la zona de juego" lo vuelve la arena.
    public sealed class ZonaDeJuego : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, .25f, .6f, .25f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(1, .001f, 1));
            Gizmos.color = new Color(1, .25f, .6f, 1);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, .001f, 1));
        }
    }
}
