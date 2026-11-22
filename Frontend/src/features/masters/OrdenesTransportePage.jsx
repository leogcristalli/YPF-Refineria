import React, { useState } from 'react';
import CrudTable from '../../common/CrudTable';
import agent from '../../api/agent';
import { Eye, CalendarClock } from 'lucide-react';
import OrdenDetailsModal from './OrdenDetailsModal';
import { OrdenFormModal, ReplanificarOrdenModal, estadoDe, esEditable } from '../planificacion/OrdenModales';

// Órdenes de Transporte (CU072-CU076). El alta y la modificación usan el mismo formulario que
// Planificación Logística; al crear se elige el tipo de orden. La fecha se cambia con Replanificar.
const OrdenesTransportePage = () => {
    const [selectedOrden, setSelectedOrden] = useState(null);
    const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
    const [isFormOpen, setIsFormOpen] = useState(false);
    const [replanificarOrden, setReplanificarOrden] = useState(null);
    const [refreshKey, setRefreshKey] = useState(0);   // al cambiar, la tabla se vuelve a cargar

    const recargar = () => setRefreshKey(prev => prev + 1);

    const handleViewDetails = (orden) => {
        setSelectedOrden(orden);
        setIsDetailModalOpen(true);
    };

    const handleCreate = () => {
        setSelectedOrden(null);
        setIsFormOpen(true);
    };

    const handleEdit = (orden) => {
        setSelectedOrden(orden);
        setIsFormOpen(true);
    };

    const columns = [
        { header: 'ID OT', field: 'id_OrdenTransporte' },
        {
            header: 'Tipo',
            field: 'ordenDetalle.orden.id_TipoOrden',
            render: (row) => (row.ordenDetalle?.orden?.id_TipoOrden === 2 ? 'Descarga' : 'Entrega')
        },
        {
            header: 'Producto',
            field: 'ordenDetalle.producto.descripcion',
            render: (row) => row.ordenDetalle?.producto?.descripcion || 'N/A'
        },
        {
            header: 'Vehículo',
            field: 'vehiculo.patenteChasis',
            render: (row) => row.vehiculo?.patenteChasis || 'N/A'
        },
        {
            header: 'Conductor',
            field: 'conductor.apellido',
            render: (row) => (row.conductor ? `${row.conductor.apellido}, ${row.conductor.nombre}` : 'N/A')
        },
        {
            header: 'Fecha',
            field: 'fechaPlanificacion',
            render: (row) => (row.fechaPlanificacion ? new Date(row.fechaPlanificacion).toLocaleDateString('es-AR') : '—')
        },
        {
            header: 'Estado',
            field: 'estadoOrdenTransporte.descripcion',
            render: (row) => estadoDe(row).texto
        },
    ];

    const actions = (row) => (
        <>
            <button
                onClick={() => handleViewDetails(row)}
                className="p-2 rounded-lg text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                title="Ver detalle"
            >
                <Eye size={16} />
            </button>
            {esEditable(row) && (
                <button
                    onClick={() => setReplanificarOrden(row)}
                    className="p-2 rounded-lg text-slate-400 hover:text-amber-600 hover:bg-amber-50 transition-all"
                    title="Replanificar"
                >
                    <CalendarClock size={16} />
                </button>
            )}
        </>
    );

    return (
        <>
            <CrudTable
                key={refreshKey}
                title="Órdenes de Transporte"
                agent={agent.OrdenesTransporte}
                columns={columns}
                idField="id_OrdenTransporte"
                actions={actions}
                onCreate={handleCreate}
                onEdit={handleEdit}
                canEdit={esEditable}
                softDelete={false}
            />

            <OrdenDetailsModal
                isOpen={isDetailModalOpen}
                onClose={() => setIsDetailModalOpen(false)}
                orden={selectedOrden}
            />

            <OrdenFormModal
                isOpen={isFormOpen}
                onClose={() => setIsFormOpen(false)}
                onSuccess={recargar}
                orden={selectedOrden}
                tipoOrden={null}
            />

            <ReplanificarOrdenModal
                isOpen={!!replanificarOrden}
                onClose={() => setReplanificarOrden(null)}
                onSuccess={recargar}
                orden={replanificarOrden}
            />
        </>
    );
};

export default OrdenesTransportePage;
