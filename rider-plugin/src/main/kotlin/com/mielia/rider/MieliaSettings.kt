package com.mielia.rider

import com.intellij.openapi.components.BaseState
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.SimplePersistentStateComponent
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.components.service
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.options.BoundConfigurable
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.ui.DialogPanel
import com.intellij.ui.dsl.builder.AlignX
import com.intellij.ui.dsl.builder.bindText
import com.intellij.ui.dsl.builder.panel

/** Which Mielia language server to run. Empty means the workspace dev build (see `locateServer`). */
@Service(Service.Level.APP)
@State(name = "MieliaSettings", storages = [Storage("mielia.xml")])
internal class MieliaSettings : SimplePersistentStateComponent<MieliaSettings.Options>(Options()) {
    class Options : BaseState() {
        var serverPath by string()
    }

    var serverPath: String
        get() = state.serverPath.orEmpty()
        set(value) {
            state.serverPath = value.trim().ifEmpty { null }
        }

    companion object {
        fun getInstance(): MieliaSettings = service()
    }
}

/** Settings | Languages & Frameworks | Mielia. */
internal class MieliaConfigurable : BoundConfigurable("Mielia") {
    private val settings = MieliaSettings.getInstance()

    override fun createPanel(): DialogPanel = panel {
        row(MieliaBundle.message("settings.label")) {
            textFieldWithBrowseButton(FileChooserDescriptorFactory.singleFile().withTitle(MieliaBundle.message("settings.title")))
                .bindText(settings::serverPath)
                .align(AlignX.FILL)
                .comment(MieliaBundle.message("settings.comment"))
        }
    }

    override fun apply() {
        super.apply()
        ProjectManager.getInstance().openProjects.forEach(MieliaLanguageServer::restart)
    }
}
