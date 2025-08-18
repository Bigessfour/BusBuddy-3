@{
    Severity     = @('Error', 'Warning')
    ExcludeRules = @(
        'PSUseDeclaredVarsMoreThanAssignments'
        'PSAvoidGlobalVars'
    )
    IncludeRules = @(
        'PSAvoidUsingWriteHost'
        'PSUseConsistentWhitespace'
        'PSUseConsistentIndentation'
        'PSUseCompatibleCmdlets'
        'PSAlignAssignmentStatement'
        'PSAvoidDefaultValueSwitchParameter'
    )
    Rules        = @{
        PSUseCompatibleCmdlets = @{ Enable = $true; TargetProfiles = @('windows_ps7_5') }
    }
}
