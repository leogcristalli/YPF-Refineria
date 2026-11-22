import React, { useState, useEffect } from 'react';
import { X, Save } from 'lucide-react';
import agent, { getErrorMessage } from '../../api/agent';

const UsuarioForm = ({ isOpen, onClose, onSuccess, initialValues }) => {
    const [loading, setLoading] = useState(false);
    const [formData, setFormData] = useState({
        idRed: '',
        nombre: '',
        apellido: '',
        nroDocumento: '',
        password: '',
        id_TipoDocumento: '1' // Defaulting to 1 for prototype
    });
    // Roles disponibles (activos) y los asignados al usuario (t_UsuarioRol).
    const [roles, setRoles] = useState([]);
    const [rolesSeleccionados, setRolesSeleccionados] = useState([]);

    useEffect(() => {
        if (!isOpen) return;
        setRolesSeleccionados([]);
        Promise.all([
            agent.Roles.list(),
            initialValues ? agent.Usuarios.roles(initialValues.id_Usuario) : Promise.resolve([]),
        ]).then(([todos, asignados]) => {
            setRoles(todos);
            setRolesSeleccionados(asignados);
        }).catch(error => alert(getErrorMessage(error, 'No se pudieron cargar los roles.')));
    }, [isOpen, initialValues]);

    const toggleRol = (idRol) => {
        setRolesSeleccionados(prev => prev.includes(idRol) ? prev.filter(id => id !== idRol) : [...prev, idRol]);
    };

    useEffect(() => {
        if (isOpen) {
            if (initialValues) {
                setFormData({
                    idRed: initialValues.idRed || '',
                    nombre: initialValues.nombre || '',
                    apellido: initialValues.apellido || '',
                    nroDocumento: initialValues.nroDocumento || '',
                    password: '',
                    id_TipoDocumento: initialValues.id_TipoDocumento || '1'
                });
            } else {
                setFormData({
                    idRed: '',
                    nombre: '',
                    apellido: '',
                    nroDocumento: '',
                    password: '',
                    id_TipoDocumento: '1'
                });
            }
        }
    }, [isOpen, initialValues]);

    const handleChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({ ...prev, [name]: value }));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        try {
            // Solo los datos editables: el backend no acepta id, activo ni hash.
            const payload = {
                idRed: formData.idRed,
                nombre: formData.nombre,
                apellido: formData.apellido,
                nroDocumento: formData.nroDocumento,
                id_TipoDocumento: parseInt(formData.id_TipoDocumento),
                password: formData.password || null,
            };

            let idUsuario;
            if (initialValues) {
                await agent.Usuarios.update(initialValues.id_Usuario, payload);
                idUsuario = initialValues.id_Usuario;
            } else {
                const creado = await agent.Usuarios.create(payload);
                idUsuario = creado.id_Usuario;
            }
            await agent.Usuarios.asignarRoles(idUsuario, rolesSeleccionados);
            onSuccess();
            onClose();
        } catch (error) {
            console.error("Error saving Usuario", error);
            alert(getErrorMessage(error, "Error al guardar Usuario."));
        } finally {
            setLoading(false);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
            <div className="bg-white rounded-lg shadow-xl w-full max-w-lg mx-4 overflow-hidden animate-in fade-in zoom-in-95 duration-200">
                <div className="bg-blue-600 px-6 py-4 flex justify-between items-center">
                    <h3 className="text-xl font-bold text-white">
                        {initialValues ? 'Editar Usuario' : 'Nuevo Usuario'}
                    </h3>
                    <button onClick={onClose} className="text-white hover:text-blue-100">
                        <X size={24} />
                    </button>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div className="grid grid-cols-2 gap-4">
                        <div className="col-span-2">
                             <label className="block text-sm font-medium text-gray-700">ID Red (Login)</label>
                            <input 
                                type="text" 
                                name="idRed"
                                value={formData.idRed}
                                onChange={handleChange}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border"
                                required
                                minLength={3}
                                maxLength={50}
                                pattern="[a-zA-Z0-9._\\\-]+"
                                title="Letras, números, puntos, guiones y \ (ej. YPF\usuario)"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-700">Nombre</label>
                            <input 
                                type="text" 
                                name="nombre"
                                value={formData.nombre}
                                onChange={handleChange}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border"
                                required
                                minLength={2}
                                maxLength={100}
                                pattern="[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ\s]+"
                                title="Solo letras y espacios"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-700">Apellido</label>
                            <input 
                                type="text" 
                                name="apellido"
                                value={formData.apellido}
                                onChange={handleChange}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border"
                                required
                                minLength={2}
                                maxLength={100}
                                pattern="[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ\s]+"
                                title="Solo letras y espacios"
                            />
                        </div>

                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-gray-700">Nro Documento</label>
                            <input 
                                type="text" 
                                name="nroDocumento"
                                value={formData.nroDocumento}
                                onChange={handleChange}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border"
                                maxLength={12}
                                required
                                minLength={6}
                                pattern="[a-zA-Z0-9]+"
                                title="Documento debe ser alfanumérico sin espacios"
                            />
                        </div>

                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-gray-700">Contraseña</label>
                            <input
                                type="password"
                                name="password"
                                value={formData.password}
                                onChange={handleChange}
                                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border"
                                placeholder={initialValues ? 'Dejar vacío para conservar la actual' : ''}
                                autoComplete="new-password"
                                required={!initialValues}
                            />
                        </div>
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Roles asignados ({rolesSeleccionados.length})
                        </label>
                        <div className="max-h-48 overflow-y-auto border border-gray-200 rounded-md divide-y divide-gray-100">
                            {roles.map((r) => (
                                <label key={r.id_Rol} className="flex items-center gap-2 px-3 py-1.5 text-sm hover:bg-gray-50">
                                    <input
                                        type="checkbox"
                                        checked={rolesSeleccionados.includes(r.id_Rol)}
                                        onChange={() => toggleRol(r.id_Rol)}
                                    />
                                    <span>{r.descripcion}</span>
                                </label>
                            ))}
                            {roles.length === 0 && (
                                <p className="px-3 py-2 text-sm text-gray-400">No hay roles disponibles.</p>
                            )}
                        </div>
                        {rolesSeleccionados.length === 0 && (
                            <p className="text-xs text-amber-600 mt-1">Sin roles, el usuario no podrá iniciar sesión.</p>
                        )}
                    </div>

                    <div className="flex justify-end pt-4 gap-3">
                        <button 
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 hover:bg-gray-50 bg "
                        >
                            Cancelar
                        </button>
                        <button 
                            type="submit"
                            disabled={loading}
                            className="px-4 py-2 bg-blue-600 border border-transparent rounded-md shadow-sm text-sm font-medium text-white hover:bg-blue-700 flex items-center gap-2"
                        >
                            <Save size={18} />
                            {loading ? 'Guardando...' : 'Guardar'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default UsuarioForm;
