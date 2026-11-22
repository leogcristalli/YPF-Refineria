import React from 'react';
import { AlertCircle, X } from 'lucide-react';

// Error visible dentro de un formulario o pantalla (en lugar de alert()).
// Con onCerrar muestra una X para descartarlo.
const MensajeError = ({ mensaje, onCerrar }) => {
    if (!mensaje) return null;
    return (
        <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-100 rounded-xl p-3 flex items-start gap-2 whitespace-pre-line">
            <AlertCircle size={16} className="shrink-0 mt-0.5" />
            <span className="flex-1">{mensaje}</span>
            {onCerrar && (
                <button type="button" onClick={onCerrar} title="Cerrar" className="text-red-400 hover:text-red-700">
                    <X size={16} />
                </button>
            )}
        </div>
    );
};

export default MensajeError;
