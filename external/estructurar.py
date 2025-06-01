import sys
import os
from pathlib import Path
from PyQt5.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QPushButton,
    QFileDialog, QListWidget, QMessageBox, QCheckBox
)

EXTENSIONES = [".cs", ".xaml", ".js", ".jsx", ".html", ".css", ".axaml", ".axaml.cs", ".sln", ".csproj"]

class FileCombiner(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Combinar Archivos Frontend y Backend")
        self.setGeometry(100, 100, 600, 400)

        self.archivos = []
        self.carpetas = []

        self.layout = QVBoxLayout()
        self.lista = QListWidget()

        self.btn_seleccionar_archivos = QPushButton("Seleccionar archivos")
        self.btn_seleccionar_archivos.clicked.connect(self.seleccionar_archivos)

        self.btn_seleccionar_carpetas = QPushButton("Seleccionar carpetas")
        self.btn_seleccionar_carpetas.clicked.connect(self.seleccionar_carpetas)

        self.checkbox = QCheckBox("Crear archivos separados por carpeta")
        self.btn_combinar = QPushButton("Combinar archivos")
        self.btn_combinar.clicked.connect(self.combinar_archivos)

        self.layout.addWidget(self.lista)
        self.layout.addWidget(self.btn_seleccionar_archivos)
        self.layout.addWidget(self.btn_seleccionar_carpetas)
        self.layout.addWidget(self.checkbox)
        self.layout.addWidget(self.btn_combinar)

        self.setLayout(self.layout)

    def seleccionar_archivos(self):
        rutas, _ = QFileDialog.getOpenFileNames(
            self,
            "Seleccionar archivos",
            "",
            "Archivos útiles (*.cs *.xaml *.js *.jsx *.html *.css *.axaml *.axaml.cs *.sln *.csproj);;Todos los archivos (*)"
        )
        for ruta in rutas:
            path = Path(ruta)
            if path not in self.archivos:
                self.archivos.append(path)
                self.lista.addItem(f"[ARCHIVO] {path}")

    def seleccionar_carpetas(self):
        while True:
            ruta = QFileDialog.getExistingDirectory(self, "Seleccionar carpeta")
            if not ruta:
                break
            path = Path(ruta)
            if path not in self.carpetas:
                self.carpetas.append(path)
                self.lista.addItem(f"[CARPETA] {path}")
            # Preguntar si quiere seguir agregando carpetas
            continuar = QMessageBox.question(
                self,
                "Agregar otra carpeta",
                "¿Deseas agregar otra carpeta?",
                QMessageBox.Yes | QMessageBox.No
            )
            if continuar == QMessageBox.No:
                break

    def recolectar_archivos(self):
        archivos_totales = list(self.archivos)
        for carpeta in self.carpetas:
            for archivo in carpeta.rglob("*"):
                if archivo.is_file() and archivo.suffix in EXTENSIONES:
                    archivos_totales.append(archivo)
        return archivos_totales

    def combinar_archivos(self):
        if not self.archivos and not self.carpetas:
            QMessageBox.warning(self, "Advertencia", "No se seleccionaron elementos.")
            return

        separar = self.checkbox.isChecked()
        try:
            todos_archivos = self.recolectar_archivos()

            if separar:
                por_carpeta = {}
                for archivo in todos_archivos:
                    carpeta = archivo.parent.name
                    por_carpeta.setdefault(carpeta, []).append(archivo)

                for carpeta, archivos in por_carpeta.items():
                    salida = Path(f"Combined_{carpeta}.txt")
                    with salida.open("w", encoding="utf-8") as f_out:
                        for archivo in archivos:
                            f_out.write(f"/// {carpeta} Start of {archivo.name} ///\n")
                            f_out.write(archivo.read_text(encoding="utf-8"))
                            f_out.write(f"\n/// {carpeta} End of {archivo.name} ///\n\n")
            else:
                salida = Path("Combined_All.txt")
                with salida.open("w", encoding="utf-8") as f_out:
                    for archivo in todos_archivos:
                        carpeta = archivo.parent.name
                        f_out.write(f"/// {carpeta} Start of {archivo.name} ///\n")
                        f_out.write(archivo.read_text(encoding="utf-8"))
                        f_out.write(f"\n/// {carpeta} End of {archivo.name} ///\n\n")

            QMessageBox.information(self, "Éxito", "Archivos combinados correctamente.")
        except Exception as e:
            QMessageBox.critical(self, "Error", str(e))


if __name__ == "__main__":
    app = QApplication(sys.argv)
    ventana = FileCombiner()
    ventana.show()
    sys.exit(app.exec_())
