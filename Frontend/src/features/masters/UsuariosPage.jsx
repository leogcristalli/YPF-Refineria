import React, { useState } from 'react';
import { KeyRound } from 'lucide-react';
import CrudTable from '../../common/CrudTable';
import agent, { getErrorMessage } from '../../api/agent';
import UsuarioForm from './UsuarioForm';

const UsuariosPage = () => {
    const [isFormOpen, setIsFormOpen] = useState(false);
    const [selectedUsuario, setSelectedUsuario] = useState(null);
    const [refreshKey, setRefreshKey] = useState(0);

    const columns = [
        { header: 'ID', field: 'id_Usuario' },
        { header: 'ID Red', field: 'idRed' },
        { header: 'Apellido', field: 'apellido' },
        { header: 'Nombre', field: 'nombre' },
        { header: 'Documento', field: 'nroDocumento' },
        { header: 'Roles', field: 'roles', render: (u) => (u.roles?.length ? u.roles.join(', ') : 'Sin roles') },
        {
            header: '2FA',
            field: 'doble2fa',
            render: (u) => (u.doble2fa
                ? <span className="text-xs font-bold text-emerald-700 bg-emerald-50 px-2 py-1 rounded-lg">Activo</span>
                : <span className="text-xs text-slate-400">Sin configurar</span>)
        },
    ];

    const handleCreate = () => {
        setSelectedUsuario(null);
        setIsFormOpen(true);
    };

    const handleEdit = (usuario) => {
        setSelectedUsuario(usuario);
        setIsFormOpen(true);
    };

    const handleSuccess = () => {
        setRefreshKey(prev => prev + 1);
        window.location.reload();
    };

    // Para quien perdió o cambió su teléfono: vuelve a configurar su autenticador al ingresar.
    const handleReiniciar2fa = async (usuario) => {
        if (!window.confirm(`¿Restablecer el doble factor de ${usuario.apellido}, ${usuario.nombre}? Tendrá que configurar su aplicación autenticadora en el próximo ingreso.`)) return;
        try {
            await agent.Usuarios.reiniciar2fa(usuario.id_Usuario);
            setRefreshKey(prev => prev + 1);
        } catch (error) {
            alert(getErrorMessage(error, 'No se pudo restablecer el doble factor.'));
        }
    };

    return (
        <>
            <CrudTable
                key={refreshKey}
                title="Gestión de Usuarios"
                agent={agent.Usuarios}
                columns={columns}
                idField="id_Usuario"
                nameField="apellido"
                onCreate={handleCreate}
                onEdit={handleEdit}
                actions={(usuario) => usuario.doble2fa && (
                    <button
                        onClick={() => handleReiniciar2fa(usuario)}
                        title="Restablecer doble factor (2FA)"
                        className="p-2 rounded-lg text-slate-400 hover:text-amber-600 hover:bg-amber-50 transition-all"
                    >
                        <KeyRound size={16} />
                    </button>
                )}
            />

            <UsuarioForm
                isOpen={isFormOpen}
                onClose={() => setIsFormOpen(false)}
                onSuccess={handleSuccess}
                initialValues={selectedUsuario}
            />
        </>
    );
};

export default UsuariosPage;
